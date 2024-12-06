using System.Text.Json;

using Amazon.SQS;
using Amazon.SQS.Model;

using FFMpegCore;
using FFMpegCore.Exceptions;
using FFMpegCore.Helpers;
using Transcoder.API.Domain.Entities;
using Transcoder.Common.Configuration;
using Transcoder.Common.MessageModels;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Serilog.Events;

using SerilogTimings;
using SerilogTimings.Extensions;
using Transcoder.API.Application.Interfaces;

namespace Transcoder.API.Application;

public class VideoService(
    Serilog.ILogger logger,
    IOptionsMonitor<QueueOptions> queueOptions,
    IAmazonSQS videoProcessingQueue,
    IApplicationDbContext dbContext) : IVideoService
{
    private readonly QueueOptions _videoQueueOptions = queueOptions.Get(QueueOptions.VideoQueue);
    private Serilog.ILogger _logger = logger;

    public async Task<Video> AddForProcessingAsync(Guid videoId, string sourcePath, string playlist, CancellationToken cancellationToken)
    {
        _logger = _logger.ForContext("VideoId", videoId);
        var video = new Video
        {
            Id = videoId,
            FileUrl = sourcePath,
            Playlist = playlist,
            Progress = 1 //initial progress as we started something
        };

        // вызвать ffprobe по filepath
        //var link = "https://s3.yandexcloud.net/ycdevmvscpcom0/4cf93c71-d5bc-4d89-97fb-08d6fa30536e/files/e029f48a-dc6a-49d1-8abd-7f2830dc3278.jpg?AWSAccessKeyId=YCAJEP_DdI0nVlHCT8RmKxaup&Expires=1721919648&Signature=4RofZ4eFxAvXOjxis7t1%2BscWU5k%3D";
        IMediaAnalysis? probe = null;

        using (var op = _logger.OperationAt(LogEventLevel.Information, LogEventLevel.Error).Begin("Initial FFProbe.AnalyseAsync"))
        {
            try
            {
                probe = await FFProbe.AnalyseAsync(new Uri(video.FileUrl), null, cancellationToken);

                FFMpegHelper.ConversionSizeExceptionCheck(probe);

                op.EnrichWith("Duration", probe.Duration.TotalSeconds);
                op.EnrichWith("Width", probe.PrimaryVideoStream!.Width);
                op.EnrichWith("Height", probe.PrimaryVideoStream.Height);

                op.Complete();
            }
            catch (FFMpegException ex)
            {
                op.Abandon(ex);
            }
            catch (ArgumentNullException ex)
            {
                probe = null;
                op.Abandon(ex);
            }
            catch (ArgumentException ex)
            {
                probe = null;
                op.Abandon(ex);
            }
        }

        if (probe == null
            || probe.Duration == TimeSpan.Zero
            || probe.PrimaryVideoStream == null
            || probe.PrimaryVideoStream.Width > 5120
            || probe.PrimaryVideoStream.Height > 5120)
        {
            video.Status = VideoStatus.Rejected;
            _logger.Warning("Video rejected due to the requirements");
        }
        else
        {


            video.Format = probe.Format.FormatName;
            video.Duration = (long)probe.Duration.TotalSeconds;
            video.Status = VideoStatus.Added;
            video.Progress = 3;

            var videoMes = new VideoMessage
            {
                VideoId = video.Id,
                FileUrl = video.FileUrl,
                Playlist = video.Playlist,
            };

            var sendMessageRequest = new SendMessageRequest
            {
                MessageBody = JsonSerializer.Serialize(videoMes),
                QueueUrl = _videoQueueOptions.Url
            };
            _logger.Information("Send video to chunker");

            await videoProcessingQueue.SendMessageAsync(sendMessageRequest, cancellationToken);
        }

        await dbContext.Videos.AddAsync(video, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return video;
    }

    public async Task<Video?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return await dbContext.Videos.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
    }
}
