using System.Diagnostics;
using System.Text.Json;
using Amazon.SQS.Model;
using FFMpegCore;
using FFMpegCore.Enums;
using Transcoder.Common;
using Microsoft.Extensions.Options;
using Transcoder.Common.Storage;
using Serilog.Context;
using Serilog.Events;
using SerilogTimings.Extensions;

namespace Transcoder.Worker;

internal sealed class Worker(
    IHostApplicationLifetime applicationLifetime,
    Serilog.ILogger logger,
    IAmazonSQS sqsClient,
    IFileStorageService fileStorageService,
    IOptionsMonitor<QueueOptions> queueOptions,
    IOptions<SharedStorageOptions> storageOptions) : WorkerBase(applicationLifetime, logger, "transcoder-worker")
{
    private readonly QueueOptions _chunkQueueOptions = queueOptions.Get(QueueOptions.ChunkQueue);
    private readonly QueueOptions _processedQueueOptions = queueOptions.Get(QueueOptions.ProcessedChunksQueue);
    private readonly Serilog.ILogger _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var request = new ReceiveMessageRequest
        {
            QueueUrl = _chunkQueueOptions.Url.ToString(),
            AttributeNames = { MessageSystemAttributeName.ApproximateReceiveCount },
            MaxNumberOfMessages = 1,
            WaitTimeSeconds = _chunkQueueOptions.WaitTimeSeconds,
            VisibilityTimeout = _chunkQueueOptions.VisibilityTimeoutSeconds
        };

        stoppingToken.Register(() =>
            _logger.Information("worker is stopping"));

        while (!stoppingToken.IsCancellationRequested)
        {
            Message sqsMessage;

            try
            {
                using (var op = _logger.OperationAt(LogEventLevel.Information, LogEventLevel.Debug).Begin("Receive message from chunks queue"))
                {
                    var response = await sqsClient.ReceiveMessageAsync(request, stoppingToken);
                    if (response.Messages.Count == 0)
                    {
                        op.Cancel();
                        continue;
                    }

                    sqsMessage = response.Messages[0];
                    op.Complete("Chunk", sqsMessage.Body);
                }

                var visibilityTimeoutTimer = Stopwatch.StartNew();
                var msgVisibilityCurrent = _chunkQueueOptions.VisibilityTimeoutSeconds;

                var chunk = JsonSerializer.Deserialize<ChunksInfoMessage>(sqsMessage.Body)!;

                using var _ = LogContext.PushProperty("VideoId", chunk.VideoId);

                var workDir = Path.Combine(storageOptions.Value.Path, chunk.VideoId.ToString());
                if (!Directory.Exists(workDir))
                {
                    Directory.CreateDirectory(workDir);
                }

                await fileStorageService.DownloadFile(Path.Combine(storageOptions.Value.Path, chunk.Key), chunk.Key, stoppingToken);

                var transcodedPath = Path.Combine(workDir, "transcoded");
                if (!Directory.Exists(transcodedPath))
                {
                    Directory.CreateDirectory(transcodedPath);
                }

                var inputPath = Path.Combine(workDir, chunk.ChunkName);

                // выбираем разрешение для кодирования
                foreach (var resolution in chunk.Resolutions)
                {
                    var transcodedChunkName = $"transcoded_{chunk.ChunkNumber}.{resolution.Height}.mp4";
                    var outputPath = Path.Combine(transcodedPath, transcodedChunkName);

                    // если с 1 попытки файл не обработался
                    if (File.Exists(outputPath))
                    {
                        File.Delete(outputPath);
                    }

                    // сохранить чанк в storage
                    var key = $"{chunk.VideoId}/transcoded/{transcodedChunkName}";

                    var sendProcessedChunkRequest = new SendMessageRequest
                    {
                        MessageBody = JsonSerializer.Serialize(new ProcessedChunkMessage
                        {
                            ChunkName = transcodedChunkName,
                            ChunkNumber = chunk.ChunkNumber,
                            Total = chunk.Total,
                            VideoId = chunk.VideoId,
                            Playlist = chunk.Playlist,
                            AudioKey = chunk.AudioKey,
                            Key = key,
                            Height = resolution.Height
                        }),
                        QueueUrl = _processedQueueOptions.Url.ToString(),
                        MessageGroupId = chunk.VideoId.ToString()
                    };

                    var args = FFMpegArguments
                        .FromFileInput(inputPath)
                        .OutputToFile(outputPath, true, options => options
                            .WithVideoCodec(VideoCodec.LibX264)
                            .WithConstantRateFactor(28)
                            .WithVideoFilters(filterOptions => filterOptions
                                .Scale(resolution.Width, resolution.Height))
                            .WithFastStart());

                    _logger.Information("Transcoding chunk #{ChunkNumber}...", chunk.ChunkNumber);

                    using (var transcodeOperation = _logger.OperationAt(LogEventLevel.Information, LogEventLevel.Error)
                               .Begin("Transcode chunk #{ChunkNumber} to {Width}px:{Height}px", chunk.ChunkNumber, resolution.Width, resolution.Height))
                    {
                        try
                        {
                            await args
                                .NotifyOnProgress(_ =>
                                {
                                    var totalSecondsElapsed = (int)visibilityTimeoutTimer.Elapsed.TotalSeconds;

                                    if (totalSecondsElapsed + 20 > msgVisibilityCurrent
                                        && totalSecondsElapsed % 10 == 0)
                                    {
                                        visibilityTimeoutTimer.Restart();
                                        msgVisibilityCurrent = 30;

                                        _logger.Information("Elapsed {Elapsed}, increasing visibility timeout for chunk #{ChunkNumber}...",
                                            totalSecondsElapsed,
                                            chunk.ChunkNumber);
                                        sqsClient.ChangeMessageVisibilityAsync(_chunkQueueOptions.Url.ToString(), sqsMessage.ReceiptHandle, msgVisibilityCurrent, stoppingToken);
                                    }
                                })
                                .ProcessAsynchronously();

                            sendProcessedChunkRequest.SetIsContinueRequestMessage();
                            transcodeOperation.Complete();
                        }
                        catch (Exception ex)
                        {
                            if (sqsMessage.GetMaxApproximateReceiveCount() >=
                                _chunkQueueOptions.MaxApproximateReceiveCount)
                            {
                                _logger.Error(ex, "Attempt {Attempt} Failed to transcode chunk #{ChunkNumber}", sqsMessage.GetMaxApproximateReceiveCount(),
                                    chunk.ChunkNumber);

                                sendProcessedChunkRequest.SetIsCancellationRequestMessage();
                                transcodeOperation.Abandon(AmazonSqsExtensions.ChunkStateMessageAttributeName, "Cancelled");
                            }
                            else
                            {
                                _logger.Warning(ex, "chunk #{ChunkNumber} failed, returning message to the queue...", chunk.ChunkNumber);
                                await sqsClient.ChangeMessageVisibilityAsync(_chunkQueueOptions.Url.ToString(), sqsMessage.ReceiptHandle, 0, stoppingToken);
                                transcodeOperation.Cancel();
                                continue;
                            }
                        }
                    }

                    _logger.Information("Uploading chunk #{ChunkNumber}...", chunk.ChunkNumber);

                    await fileStorageService
                        .UploadFileToTranscoderBucket(outputPath, key, stoppingToken);

                    _logger.Information("Finished chunk #{ChunkNumber} processing", chunk.ChunkNumber);

                    await sqsClient.DeleteMessageAsync(new DeleteMessageRequest(_chunkQueueOptions.Url.ToString(), sqsMessage.ReceiptHandle),
                        stoppingToken);

                    await sqsClient.SendMessageAsync(sendProcessedChunkRequest, stoppingToken);
                }
            }
            catch (OperationCanceledException ex)
            {
                _logger.Warning(ex, "Scale down event occured");
            }
#pragma warning disable CA1031
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.Fatal(ex, "transcoder-worker failed");
            }
            finally
            {
                CleanUp(storageOptions.Value.Path);
            }
        }
    }
}
