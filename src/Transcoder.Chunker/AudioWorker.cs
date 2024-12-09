using FFMpegCore;
using FFMpegCore.Exceptions;
using Transcoder.Common;
using Transcoder.Common.Configuration;
using Transcoder.Common.Storage;

using Microsoft.Extensions.Options;

using Serilog.Context;
using SerilogTimings;
using SerilogTimings.Extensions;
using Transcoder.Chunker.Interfaces;
using Transcoder.Chunker.Models;

namespace Transcoder.Chunker;

internal class AudioWorker(
    IHostApplicationLifetime applicationLifetime,
    IBackgroundTaskQueue<AudioWorkItem> taskQueue,
    Serilog.ILogger logger,
    FileStorageService fileService,
    IOptions<SharedStorageOptions> storageOptions)
    : WorkerBase(applicationLifetime, logger, "transcoder-chunker-audio")
{
    private readonly Serilog.ILogger _logger = logger;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return ProcessTaskQueueAsync(stoppingToken);
    }

    private async Task ProcessTaskQueueAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var workItem = await taskQueue.ConsumeAsync(stoppingToken);
                _ = LogContext.PushProperty("VideoId", workItem.VideoId);

                _logger.Information("Extracting audio...");

                using var extractAudioOperation = _logger.BeginOperation("Extract audio");
                try
                {
                    var audioPath = Path.Combine(storageOptions.Value.Path, "audio", workItem.VideoId.ToString());
                    var audioFile = Path.Combine(audioPath, workItem.AudioName);
                    var videoForAudio = Path.Combine(audioPath, Path.GetFileName(workItem.OriginalVideoFilePath));

                    Directory.CreateDirectory(audioPath);

                    File.Copy(workItem.OriginalVideoFilePath, videoForAudio);
                    FFMpeg.ExtractAudio(videoForAudio, audioFile );
                    await fileService.UploadFileToTranscoderBucket(audioFile, workItem.UploadObjectKey, stoppingToken);

                    Directory.Delete(audioPath, true);

                    extractAudioOperation.Complete();
                }
                catch (FFMpegException ex)
                {
                    extractAudioOperation.Abandon(ex);
                }
            }
            catch (OperationCanceledException)
            {
                // Prevent throwing if the Delay is cancelled
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error occurred executing audio work item");
            }
        }
    }
}
