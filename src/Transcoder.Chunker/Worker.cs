using System.Diagnostics;
using System.Drawing;
using System.Text.Json;

using Amazon.SQS;
using Amazon.SQS.Model;

using FFMpegCore;
using FFMpegCore.Exceptions;
using FFMpegCore.Helpers;

using Transcoder.Common;
using Transcoder.Common.Configuration;
using Transcoder.Common.MessageModels;
using Transcoder.Common.Storage;

using Microsoft.Extensions.Options;

using Serilog.Context;
using Serilog.Events;

using SerilogTimings;
using SerilogTimings.Extensions;

namespace Transcoder.Chunker;

public class Worker(
    IHostApplicationLifetime applicationLifetime,
#if !SKIP_AUDIO
    IBackgroundTaskQueue<AudioWorkItem> taskQueue,
#endif
    Serilog.ILogger logger,
    IAmazonSQS sqsClient,
    IOptionsMonitor<QueueOptions> queueOptions,
    IOptions<SharedStorageOptions> storageOptions,
    IOptions<ProcessingOptions> processingOptions,
    StatusSender statusSender,
    IFileStorageService fileService)
    : WorkerBase(applicationLifetime, logger, "transcoder-chunker")
{
    private readonly QueueOptions _chunkQueueOptions = queueOptions.Get(QueueOptions.ChunkQueue);
    private readonly QueueOptions _videoQueueOptions = queueOptions.Get(QueueOptions.VideoQueue);
    private readonly ProcessingOptions _processingOptions = processingOptions.Value;

    private readonly string _coverName = "cover.png";
    private readonly Serilog.ILogger _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var request = new ReceiveMessageRequest
        {
            QueueUrl = _videoQueueOptions.Url.ToString(),
            MaxNumberOfMessages = 1,
            WaitTimeSeconds = _videoQueueOptions.WaitTimeSeconds,
            VisibilityTimeout = _videoQueueOptions.VisibilityTimeoutSeconds
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            ReceiveMessageResponse response;

            using (var op = _logger.OperationAt(LogEventLevel.Information, LogEventLevel.Debug).Begin("Receive messages from video queue"))
            {
                response = await sqsClient.ReceiveMessageAsync(request, stoppingToken);
                if (response.Messages.Count == 0)
                {
                    op.Cancel();
                    continue;
                }

                op.Complete("NumberOfMessages", response.Messages.Count);
            }

            var visibilityTimeoutTimer = Stopwatch.StartNew();
            var msgVisibilityCurrent = _videoQueueOptions.VisibilityTimeoutSeconds;

            var videoId = Guid.Empty;

            foreach (var sqsMessage in response.Messages)
            {
                try
                {
                    var videoToProcess = JsonSerializer.Deserialize<VideoMessage>(sqsMessage.Body)!;

                    videoId = videoToProcess.VideoId;
                    var fileUrl = new Uri(videoToProcess.FileUrl.ToString());
                    var workingDir = Path.Combine(storageOptions.Value.Path, videoId.ToString(), videoId.ToString()); //лишний videoId для копирования папки

                    Directory.CreateDirectory(workingDir);
                    var fileName = Path.GetFileName(fileUrl.LocalPath);
                    var fileExt = Path.GetExtension(fileUrl.LocalPath);
                    var filePath = Path.Combine(workingDir,
                        fileName); // изменять fileName, чтобы не было конфликта папок, если будут выбраны одинаковые файлы для транскодинга
                    string? audioKey = null;

                    using var _ = LogContext.PushProperty("VideoId", videoId);

                    _logger.Information("Downloading...");
                    using (_logger.OperationAt(LogEventLevel.Information, LogEventLevel.Error).Time("Download video from {@FileUrl}", fileUrl))
                    {
                        using var client = new HttpClient();
                        using var s = client.GetStreamAsync(fileUrl, stoppingToken);
                        await using var fs = new FileStream(filePath, FileMode.OpenOrCreate);
                        await s.Result.CopyToAsync(fs, stoppingToken);
                    }

                    var probe = await FFProbe.AnalyseAsync(filePath, cancellationToken: stoppingToken);

                    var pathImg = Path.Combine(storageOptions.Value.Path, "finished", videoId.ToString());
                    if (!Directory.Exists(pathImg))
                    {
                        Directory.CreateDirectory(pathImg);
                    }

                    // выбираем разрешения для чанков

                    List<Resolution> allSizes = [];
                    var origWidth = probe.PrimaryVideoStream!.Width;
                    var origHeight = probe.PrimaryVideoStream!.Height;

                    var origResolution = origWidth * origHeight;
                    var origAspectRatio = probe.PrimaryVideoStream!.DisplayAspectRatio;
                    if (origAspectRatio.Width == 0 || origAspectRatio.Height == 0)
                    {
                        origAspectRatio = ResolutionCalculator.CalculateAspectRatio(origWidth, origHeight);
                    }
                    var baseSize = ResolutionCalculator.ChooseBaseSize(
                        origAspectRatio.Width, origAspectRatio.Height, _processingOptions.UseSdBaseVideoSize);
                    var baseResolution = _processingOptions.UseSdBaseVideoSize ? ResolutionCalculator.SdResolution : ResolutionCalculator.HdResolution;

                    // размер видео даже меньше
                    if (baseSize == default || origResolution < baseResolution)
                    {
                        baseSize = (origWidth, origHeight);
                        allSizes.Add(new Resolution(baseSize.Width, baseSize.Height));
                    }
                    else
                    {
                        var sizes = ResolutionCalculator.CalculateSizes(origAspectRatio.Width, origAspectRatio.Height);

                        allSizes.AddRange(sizes
                            .Where(size => size != default
                                           && size.Width <= origWidth && size.Height <= origHeight)
                            .OrderBy(size => size.Height)
                            .Select(size => new Resolution(size.Width, size.Height)));
                    }

                    _logger.Information("Initial available sizes {@Sizes}", allSizes);

                    if (_processingOptions.UseSingleVideoSize)
                    {
                        _logger.Information("Use single size as {Size}", baseSize);

                        allSizes.RemoveAll(size => size.Height != baseSize.Height);
                    }

                    ArgumentOutOfRangeException.ThrowIfZero(allSizes.Count, $"{nameof(allSizes)} is empty");

                    FFMpegHelper.ConversionSizeExceptionCheck(baseSize.Width, baseSize.Height);

                    // делаем обложку
                    _logger.Information("Generating cover...");
                    using (var operationCover = Operation.Begin("Generate cover"))
                    {
                        try
                        {
                            await FFMpeg.SnapshotAsync(filePath, Path.Combine(pathImg, "cover"), new Size(baseSize.Width, baseSize.Height));
                            await fileService.UploadFileToContentBucket(Path.Combine(pathImg, _coverName), $"{videoId}/{_coverName}", stoppingToken);
                            operationCover.Complete();
                        }
                        catch (Exception ex)
                        {
                            _logger.Warning(ex, "Failed to generate cover");
                            operationCover.Abandon(ex);
                        }
                    }

                    // отрезаем аудио
                    if (probe.AudioStreams.Count != 0)
                    {
#if !SKIP_AUDIO
                        const string audioFileName = "audio.mp3";
                        audioKey = $"{videoId}/{audioFileName}";
                        await taskQueue.ProduceAsync(new AudioWorkItem(
                            videoId,
                            filePath,
                            audioFileName,
                            audioKey
                        ));
#endif

                        // TODO:
                        // .NotifyOnProgress(_ =>
                        // {
                        //     var totalSecondsElapsed = visibilityTimeoutTimer.Elapsed.TotalSeconds;
                        //
                        //     if (totalSecondsElapsed + 12 > _videoQueueOptions.VisibilityTimeoutSeconds
                        //         && totalSecondsElapsed % 10 == 0)
                        //     {
                        //         _logger.Information("Elapsed {Elapsed}, increasing visibility timeout...",
                        //             totalSecondsElapsed);
                        //         sqsClient.ChangeMessageVisibilityAsync(_videoQueueOptions.Url, sqsMessage.ReceiptHandle, 10, stoppingToken);
                        //     }
                    }

                    var listOfChunks = new List<ChunksInfoMessage>();
                    int i;

                    // делим видео на чанки. длительность чанка зависит от разрешения, чтобы чанк мог обработаться
                    int scaler = origResolution / ResolutionCalculator.FullHdResolution;
                    scaler = scaler switch
                    {
                        <= 0 => 1,
                        > 4 => 4,
                        _ => scaler
                    };

                    int step = 60 / scaler;

                    _logger.Information("Chunking video per {Duration} seconds ...", step);

                    // делим видео на длительность
                    using (var operationChunks = _logger.BeginOperation("Chunk video"))
                    {
                        for (i = 0; probe.Duration.TotalSeconds - i * step > 0; i++)
                        {
                            var chunkName = $"chunk_{i}{fileExt}";
                            var startTime = TimeSpan.FromSeconds(i * step);
                            var endTime = probe.Duration.TotalSeconds - i * step > step ? TimeSpan.FromSeconds((i + 1) * step) : probe.Duration;
                            try
                            {
                                var input = filePath;
                                var output = Path.Combine(workingDir, chunkName);
                                if (Path.GetExtension(input) != Path.GetExtension(output))
                                {
                                    output = Path.Combine(Path.GetDirectoryName(output)!, Path.GetFileNameWithoutExtension(output), Path.GetExtension(input));
                                }

                                await FFMpegArguments
                                    .FromFileInput(input,
                                        addArguments: (Action<FFMpegArgumentOptions>)(options =>
                                            options
                                                .Seek(startTime)
                                                .EndSeek(endTime)))
                                    .OutputToFile(output,
                                        addArguments: (Action<FFMpegArgumentOptions>)(options =>
                                        {
#if !SKIP_AUDIO
                                            options.CopyChannel(Channel.Video);
#endif
                                            options.CopyChannel();
                                        }))
                                    .NotifyOnProgress(_ =>
                                    {
                                        var totalSecondsElapsed = (int)visibilityTimeoutTimer.Elapsed.TotalSeconds;

                                        if (totalSecondsElapsed + 20 > msgVisibilityCurrent
                                            && totalSecondsElapsed % 10 == 0)
                                        {
                                            visibilityTimeoutTimer.Restart();
                                            msgVisibilityCurrent = 60;

                                            _logger.Information("Elapsed {Elapsed}, increasing visibility timeout for chunking...",
                                                totalSecondsElapsed);
                                            sqsClient.ChangeMessageVisibilityAsync(_videoQueueOptions.Url.ToString(), sqsMessage.ReceiptHandle, msgVisibilityCurrent,
                                                stoppingToken);
                                        }
                                    })
                                    .ProcessAsynchronously();

                                listOfChunks.Add(new ChunksInfoMessage
                                {
                                    ChunkNumber = i,
#if !SKIP_AUDIO
                                    AudioKey = audioKey,
#endif
                                    ChunkName = chunkName,
                                    VideoId = videoId,
                                    Playlist = videoToProcess.Playlist,
                                    Resolutions = allSizes,
                                    Duration = endTime.Subtract(startTime).TotalSeconds,
                                    Key = $"{videoId}/{chunkName}"
                                });
                            }
                            catch (FFMpegException ex)
                            {
                                operationChunks.Abandon(ex);
                            }
                        }

                        listOfChunks.ForEach(chunk =>
                        {
                            chunk.Total = i;
                        });

                        operationChunks.Complete("NumberOfChunks", i);
                    }

                    _logger.Information("Uploading chunks to bucket...");
                    using (var uploadChunksOperation = _logger.BeginOperation("Upload chunks to bucket"))
                    {
                        await fileService.UploadDirectoryToTranscoderBucket(
                            Path.Combine(storageOptions.Value.Path, videoId.ToString()),
                            (sender, args) =>
                            {
                                var totalSecondsElapsed = (int)visibilityTimeoutTimer.Elapsed.TotalSeconds;

                                if (totalSecondsElapsed + 20 > msgVisibilityCurrent
                                    && totalSecondsElapsed % 10 == 0)
                                {
                                    visibilityTimeoutTimer.Restart();
                                    msgVisibilityCurrent = 60;

                                    _logger.Information("Elapsed {Elapsed}, increasing visibility timeout for upload...",
                                        totalSecondsElapsed);
                                    sqsClient.ChangeMessageVisibilityAsync(_videoQueueOptions.Url.ToString(), sqsMessage.ReceiptHandle, msgVisibilityCurrent,
                                        stoppingToken);
                                }
                            },
                            stoppingToken);

                        uploadChunksOperation.Complete();
                    }

                    _logger.Information("Sending to chunks queue...");
                    using (_logger.TimeOperation("Send batches to chunk queue"))
                    {
                        await statusSender.SendResolutionBatchStatus(
                            videoId,
                            VideoStatus.Chopped,
                            allSizes,
                            sqsClient,
                            stoppingToken);

                        var messagesWithChunkInfo = listOfChunks.Select(chunk =>
                            new SendMessageBatchRequestEntry
                            {
                                Id = chunk.ChunkName,
                                MessageBody = JsonSerializer.Serialize(chunk)
                            }
                        ).ToList();
                        var chunksForBatch = messagesWithChunkInfo.Chunk(10);
                        foreach (var chunk in chunksForBatch)
                        {
                            await sqsClient.SendMessageBatchAsync(_chunkQueueOptions.Url.ToString(), [.. chunk], stoppingToken);
                        }
                    }

                    await sqsClient.DeleteMessageAsync(_videoQueueOptions.Url.ToString(), sqsMessage.ReceiptHandle, stoppingToken);
                }
                catch (OperationCanceledException ex)
                {
                    _logger.Warning(ex, "Scale down event occured");
                }
                catch (Exception ex)
                {
                    await statusSender.SendStatus(videoId, VideoStatus.Rejected, sqsClient, stoppingToken);
                    await sqsClient.DeleteMessageAsync(_videoQueueOptions.Url.ToString(), sqsMessage.ReceiptHandle, stoppingToken);
                    _logger.Error(ex, "Failed processing video");
                }
                finally
                {
                    CleanUp(storageOptions.Value.Path);
                }
            }
        }
    }
}
