using System.Text.Json;

using Amazon.SQS;
using Amazon.SQS.Model;

using Transcoder.API.Application.Interfaces;
using Transcoder.Common;
using Transcoder.Common.Configuration;
using Transcoder.Common.MessageModels;

using Microsoft.Extensions.Options;

using Serilog.Context;
using Serilog.Events;

using SerilogTimings;
using SerilogTimings.Extensions;

namespace Transcoder.API;

internal class Worker(
    IHostApplicationLifetime applicationLifetime,
    Serilog.ILogger logger,
    IServiceProvider services,
    IOptionsMonitor<QueueOptions> queueOptions,
    IAmazonSQS sqsClient)
    : WorkerBase(applicationLifetime, logger, "transcoder-api")
{
    private readonly QueueOptions _processingStatusQueueOptions = queueOptions.Get(QueueOptions.StatusQueue);
    private readonly Serilog.ILogger _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        var request = new ReceiveMessageRequest
        {
            QueueUrl = _processingStatusQueueOptions.Url.ToString(),
            AttributeNames = { MessageSystemAttributeName.ApproximateReceiveCount },
            MaxNumberOfMessages = 1,
            WaitTimeSeconds = _processingStatusQueueOptions.WaitTimeSeconds,
            VisibilityTimeout = _processingStatusQueueOptions.VisibilityTimeoutSeconds
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

            ReceiveMessageResponse response;

            using (var op = _logger.OperationAt(LogEventLevel.Information, LogEventLevel.Debug).Begin("Receive message from status queue"))
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
                foreach (var sqsMessage in response.Messages)
                {
                    var message = JsonSerializer.Deserialize<ProcessingStatusMessage>(sqsMessage.Body)!;

                    using var _ = LogContext.PushProperty("VideoId", message.VideoId);
                    using var op = Operation.Begin("Process status");

                    var video = await dbContext.Videos.FindAsync([message.VideoId], cancellationToken: stoppingToken);

                    if (video == null)
                    {
                        //что-то странное, поэтому удаляем сообщение
                        await sqsClient.DeleteMessageAsync(new DeleteMessageRequest(_processingStatusQueueOptions.Url.ToString(), sqsMessage.ReceiptHandle),
                            stoppingToken);
                        op.Abandon("Status", "NotFound");
                        continue;
                    }

                    switch (message.Status)
                    {
                        case VideoStatus.Processing:
                            video.ResolutionProgress.First(x => x.Height == message.Height).Progress = message.ResolutionProgress;

                            if (video.Status <= VideoStatus.Processing)
                            {
                                video.Status = VideoStatus.Processing;

                                // прогресс конкретного разрешения мб 100%, но в общем процессе дает не больше 90
                                var resProgress = video.ResolutionProgress.OrderByDescending(x => x.Progress).First().Progress * 90 / 100;
                                video.Progress = resProgress > video.Progress ? resProgress : video.Progress;
                            }
                            break;
                        case VideoStatus.Processed:
                            video.ResolutionProgress.First(x => x.Height == message.Height).Progress = 100;

                            if (video.Status <= VideoStatus.Processed)
                            {
                                video.Status = VideoStatus.Processed;
                                video.Progress = 90;
                            }
                            break;
                        case VideoStatus.Done:
                            video.Status = VideoStatus.Done;
                            video.Progress = 100;
                            break;
                        case VideoStatus.Chopped:
                            if (video.Status <= VideoStatus.Chopped)
                            {
                                video.Status = VideoStatus.Chopped;
                                video.Progress = 7;
                            }
                            video.ResolutionProgress.Add(new Domain.Entities.ResolutionProgress()
                            {
                                Height = message.Height,
                                Progress = message.ResolutionProgress
                            });
                            break;
                        case VideoStatus.Rejected:
                            if (video.Status != VideoStatus.Done)
                            {
                                video.Status = VideoStatus.Rejected;
                            }
                            break;
                        case VideoStatus.Added:
                        case VideoStatus.Verified:
                        case VideoStatus.Gluing:
                        default:
                            break;
                    }

                    video.UpdateDate = DateTime.UtcNow;
                    await dbContext.SaveChangesAsync(stoppingToken);

                    await sqsClient.DeleteMessageAsync(new DeleteMessageRequest(_processingStatusQueueOptions.Url.ToString(), sqsMessage.ReceiptHandle),
                        stoppingToken);

                    op.EnrichWith("Progress", video.ResolutionProgress);
                    op.EnrichWith("ElapsedSinceCreated", Math.Round((video.UpdateDate - video.CreateDate).TotalSeconds));
                    if (video.Status == VideoStatus.Rejected)
                    {
                        op.Abandon("Status", video.Status);
                    }
                    else
                    {
                        op.Complete("Status", video.Status);
                    }
                }
            }
            catch (AmazonSQSException ex)
            {
                _logger.Fatal(ex, "Can't access status queue");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed updating status");
            }
        }
    }
}
