using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using FFMpegCore;
using FFMpegCore.Exceptions;
using Transcoder.Common;
using Transcoder.Common.Configuration;
using Transcoder.Common.MessageModels;
using Transcoder.Common.Storage;
using Transcoder.Composer.Application.Interfaces;
using Transcoder.Composer.Application.Services;
using Transcoder.Composer.Domain;
using Microsoft.Extensions.Options;
using Serilog.Context;
using Serilog.Events;
using SerilogTimings;
using SerilogTimings.Extensions;

namespace Transcoder.Composer;

public class Worker(
    IHostApplicationLifetime applicationLifetime,
    Serilog.ILogger logger,
    IServiceProvider services,
    IAmazonSQS sqsClient,
    IOptionsMonitor<QueueOptions> queueOptions,
    IOptions<SharedStorageOptions> storageOptions,
    StatusSender statusSender,
    IFileStorageService fileStorageService)
    : WorkerBase(applicationLifetime, logger, "transcoder-composer")
{
    private readonly QueueOptions _streamQueueOptions = queueOptions.Get(QueueOptions.StreamQueue);
    private readonly Serilog.ILogger _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var request = new ReceiveMessageRequest
        {
            QueueUrl = _streamQueueOptions.Url.ToString(),
            AttributeNames = { MessageSystemAttributeName.ApproximateReceiveCount },
            MaxNumberOfMessages = 1,
            WaitTimeSeconds = _streamQueueOptions.WaitTimeSeconds,
            VisibilityTimeout = _streamQueueOptions.VisibilityTimeoutSeconds
        };

        stoppingToken.Register(() =>
            _logger.Information("worker is stopping"));

        while (!stoppingToken.IsCancellationRequested)
        {
            ReceiveMessageResponse response;
            using (var op = _logger.OperationAt(LogEventLevel.Information, LogEventLevel.Debug).Begin("Receive message from stream queue"))
            {
                response = await sqsClient.ReceiveMessageAsync(request, stoppingToken);
                if (response.Messages.Count == 0)
                {
                    op.Cancel();
                    continue;
                }

                op.Complete("NumberOfMessages", response.Messages.Count);
            }

            try
            {
                using var scope = services.CreateScope();
                var chunksRepository = scope.ServiceProvider.GetRequiredService<IChunkRepository>();
                var videoProfilesRepository = scope.ServiceProvider.GetRequiredService<IVideoProfilesRepository>();

                var visibilityTimeoutTimer = Stopwatch.StartNew();
                var msgVisibilityCurrent = _streamQueueOptions.VisibilityTimeoutSeconds;

                var sqsMessage = response.Messages[0];

                var message = JsonSerializer.Deserialize<CollectChunksMessage>(sqsMessage.Body)!;

                using var _ = LogContext.PushProperty("VideoId", message.VideoId);

                var transcodedPath = Path.Combine(storageOptions.Value.Path, message.VideoId.ToString(), "transcoded");
                var finishedPath = Path.Combine(storageOptions.Value.Path, "finished", message.VideoId.ToString(), message.VideoId.ToString());

                var chunks = (await chunksRepository.GetAllAsync(message.VideoId, message.Height, stoppingToken))
                    .DistinctBy(x => x.ChunkNumber).OrderBy(x => x.ChunkNumber).ToList();

                if (chunks.Count == 0)
                {
                    _logger.Warning("No chunks found, duplicate message is detected");
                    await sqsClient.DeleteMessageAsync(new DeleteMessageRequest(_streamQueueOptions.Url.ToString(), sqsMessage.ReceiptHandle),
                        stoppingToken);

                    continue;
                }

                _logger.Information("Downloading chunks and audio...");

                var audioKey = chunks.Single(x => x.ChunkNumber == 0).AudioKey;
                var audioFilePath = audioKey == null ? null : Path.Combine(storageOptions.Value.Path, audioKey);

                using (_logger.TimeOperation("Download chunks and audio"))
                {
                    if (!Directory.Exists(transcodedPath))
                    {
                        Directory.CreateDirectory(transcodedPath);
                    }

                    foreach (var chunk in chunks)
                    {
                        await fileStorageService.DownloadFile(Path.Combine(transcodedPath, chunk.ChunkName!), chunk.Key!, stoppingToken);
                    }

                    if (audioKey != null)
                    {
                        _logger.Information("Audio exists at {AudioPath}...", audioKey);
                        await fileStorageService.DownloadFile(audioFilePath!, audioKey, stoppingToken);
                    }
                }

                _logger.Information("Joining chunks, creating stream file...");
                var fileName = $"{message.Height}.mp4";
                using (var joinChunksOperation = _logger.OperationAt(LogEventLevel.Information, LogEventLevel.Error)
                           .Begin("FFMpeg.Join {NumberOfChunks} chunks", chunks.Count))
                {
                    if (!Directory.Exists(finishedPath))
                    {
                        Directory.CreateDirectory(finishedPath);
                    }

                    var videos = chunks.Select(x => Path.Combine(transcodedPath, x.ChunkName!)).ToArray();

                    var outputPath = Path.Combine(File.Exists(audioFilePath) ? transcodedPath : finishedPath, fileName);

                    try
                    {
                        FFMpeg.Join(outputPath, videos);

                        var resultffprobe = FFProbe.AnalyseAsync(outputPath, cancellationToken: stoppingToken);
                        joinChunksOperation.Complete("Ffprobe", resultffprobe, true);
                    }
                    catch (Exception ex) when (ex is FFMpegException or NullReferenceException)
                    {
                        if (sqsMessage.GetMaxApproximateReceiveCount() >=
                            _streamQueueOptions.MaxApproximateReceiveCount)
                        {
                            await statusSender.SendStatus(message.VideoId, VideoStatus.Rejected, sqsClient, stoppingToken);
                            await sqsClient.DeleteMessageAsync(new DeleteMessageRequest(_streamQueueOptions.Url.ToString(), sqsMessage.ReceiptHandle),
                                stoppingToken);
                            joinChunksOperation.Abandon(ex);
                        }
                        else
                        {
                            _logger.Warning(ex, "Probably on of the chunk files is incomplete, returning message to the queue...");
                            await sqsClient.ChangeMessageVisibilityAsync(_streamQueueOptions.Url.ToString(), sqsMessage.ReceiptHandle, 0, stoppingToken);
                            joinChunksOperation.Cancel();
                            continue;
                        }
                    }
                }

                var finishFilePath = Path.Combine(finishedPath, fileName);
                if (File.Exists(audioFilePath))
                {
                    _logger.Information("Replacing audio...");

                    // checkpoint
                    if (visibilityTimeoutTimer.Elapsed.TotalSeconds + 12 > _streamQueueOptions.VisibilityTimeoutSeconds)
                    {
                        _logger.Information("Elapsed {Elapsed}, increasing visibility timeout...",
                            visibilityTimeoutTimer.Elapsed.TotalSeconds);
                        await sqsClient.ChangeMessageVisibilityAsync(_streamQueueOptions.Url.ToString(),
                            sqsMessage.ReceiptHandle,
                            _streamQueueOptions.VisibilityTimeoutSeconds, stoppingToken);
                    }

                    using var replaceAudioOperation = _logger.BeginOperation("Replace audio");
                    try
                    {
                        FFMpeg.ReplaceAudio(Path.Combine(transcodedPath, fileName), audioFilePath, finishFilePath);
                        replaceAudioOperation.Complete();
                    }
                    catch (FFMpegException ex)
                    {
                        replaceAudioOperation.Abandon(ex);
                    }
                }

                /*// выгрузить затранскоженный видос
                _logger.Information("Upload file {Height}.mp4 ...", message.Height);

                await fileStorageService.UploadFile(finishFilePath, $"{message.VideoId}/finished/{fileName}", stoppingToken);
                */

                // создать новый профиль по videoId
                var probeTranscoded = await FFProbe.AnalyseAsync(finishFilePath, cancellationToken: stoppingToken);
                var profile = new VideoProfile
                {
                    VideoId = message.VideoId,
                    Width = probeTranscoded.PrimaryVideoStream!.Width,
                    Height = probeTranscoded.PrimaryVideoStream.Height,
                    BitRate = probeTranscoded.PrimaryVideoStream.BitRate,
                    Codec = File.Exists(audioFilePath) ? "avc1.42001f,mp4a.40.2" : "avc1.42001f"
                };

                await videoProfilesRepository.AddAsync(profile, stoppingToken);

                _logger.Information("Creating media container...");

                using (var createMediaContainerOperation = _logger.OperationAt(LogEventLevel.Information, LogEventLevel.Error).Begin("Create media container"))
                {
                    try
                    {
                        await FFMpegArguments
                            .FromFileInput(finishFilePath)
                            .OutputToFile(Path.Combine(finishedPath, $"{message.Height}.m3u8"), true, options => options
                                .WithArgument(new HlsArguments())
                                .WithFastStart())
                            .NotifyOnProgress(_ =>
                            {
                                var totalSecondsElapsed = (int)visibilityTimeoutTimer.Elapsed.TotalSeconds;

                                if (totalSecondsElapsed + 20 > msgVisibilityCurrent
                                    && totalSecondsElapsed % 10 == 0)
                                {
                                    visibilityTimeoutTimer.Restart();
                                    msgVisibilityCurrent = 60;

                                    _logger.Information("Elapsed {Elapsed}, increasing visibility timeout for media container...",
                                        totalSecondsElapsed);
                                    sqsClient.ChangeMessageVisibilityAsync(_streamQueueOptions.Url.ToString(), sqsMessage.ReceiptHandle, msgVisibilityCurrent, stoppingToken);
                                }
                            })
                            .ProcessAsynchronously();

                        createMediaContainerOperation.Complete();
                    }
                    catch (FFMpegException ex)
                    {
                        await statusSender.SendStatus(message.VideoId, VideoStatus.Rejected, sqsClient, stoppingToken);
                        await sqsClient.DeleteMessageAsync(new DeleteMessageRequest(_streamQueueOptions.Url.ToString(), sqsMessage.ReceiptHandle),
                            stoppingToken);
                        createMediaContainerOperation.Abandon(ex);
                        continue;
                    }
                }

                File.Delete(finishFilePath);

                // создать master playlist

                var profiles = await videoProfilesRepository.GetAllAsync(message.VideoId, stoppingToken);
                var masterPath = Path.Combine(finishedPath, message.Playlist!);
                await GenerateMasterPlaylist(profiles, masterPath);

                // загрузить в s3
                _logger.Information("Uploading container files to public bucket...");

                using (_logger.TimeOperation("Upload container files to public bucket"))
                {
                    await fileStorageService.UploadDirectoryToContentBucket(
                        Path.Combine(storageOptions.Value.Path, "finished", message.VideoId.ToString()),
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
                                sqsClient.ChangeMessageVisibilityAsync(_streamQueueOptions.Url.ToString(), sqsMessage.ReceiptHandle, msgVisibilityCurrent,
                                    stoppingToken);
                            }
                        },
                        stoppingToken);
                }

                var segmentsCount = PlaylistExtensions.GetPlaylistFilesCount(finishedPath);

                _logger.Information("Finished processing");

                await sqsClient.DeleteMessageAsync(new DeleteMessageRequest(_streamQueueOptions.Url.ToString(), sqsMessage.ReceiptHandle),
                    stoppingToken);
                await statusSender.SendStatus(message.VideoId, VideoStatus.Done, sqsClient, stoppingToken, segmentsCount);

                await chunksRepository.DeleteChunks([.. await chunksRepository.GetAllAsync(message.VideoId, stoppingToken)], stoppingToken);
            }

            catch (OperationCanceledException ex)
            {
                _logger.Warning(ex, "Scale down event occured");
            }
            catch (Exception ex)
            {
                _logger.Fatal(ex, "composer-worker failed");
            }
            finally
            {
                CleanUp(storageOptions.Value.Path);
            }
        }
    }

    private static async Task GenerateMasterPlaylist(List<VideoProfile> profiles, string path)
    {
        var mainPlaylist = new StringBuilder();
        mainPlaylist.Append("#EXTM3U\n#EXT-X-VERSION:3");
        mainPlaylist.Append('\n');
        foreach (var profile in profiles)
        {
            var playListName = $"{profile.Height}.m3u8";
            mainPlaylist.Append("#EXT-X-STREAM-INF:RESOLUTION=");
            mainPlaylist.Append(profile.Width);
            mainPlaylist.Append('x');
            mainPlaylist.Append(profile.Height);
            mainPlaylist.Append(",BANDWIDTH=");
            mainPlaylist.Append((int)(profile.BitRate * 1.1));
            mainPlaylist.Append(",CODECS=\"");
            mainPlaylist.Append(profile.Codec);
            mainPlaylist.Append('"');
            mainPlaylist.Append('\n');
            mainPlaylist.Append(playListName);
            mainPlaylist.Append('\n');
        }

        await File.WriteAllTextAsync(path, mainPlaylist.ToString());
    }
}
