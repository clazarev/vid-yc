using System.Collections.ObjectModel;
using Amazon.SQS.Model;
using Amazon.SQS;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Transcoder.Common.Configuration;
using Transcoder.Common.MessageModels;

namespace Transcoder.Common;

public class StatusSender(
    IOptionsMonitor<QueueOptions> queueOptions)
{
    private readonly QueueOptions _processingStatusQueueOptions = queueOptions.Get(QueueOptions.StatusQueue);
    public async Task SendStatus(Guid videoId, VideoStatus status, IAmazonSQS sqsClient, CancellationToken stoppingToken, int height = 0, int resolutionProgress = 0, int filesCount = 0)
    {
        var sendRequest = new SendMessageRequest
        {
            MessageBody = JsonSerializer.Serialize(new ProcessingStatusMessage
            {
                VideoId = videoId,
                Status = status,
                Files = filesCount,
                Height = height,
                ResolutionProgress = resolutionProgress
            }),
            QueueUrl = _processingStatusQueueOptions.Url.ToString(),
        };
        await sqsClient.SendMessageAsync(sendRequest, stoppingToken);
    }

    public async Task SendResolutionBatchStatus(Guid videoId, VideoStatus status, ReadOnlyCollection<Resolution> resolutions, IAmazonSQS sqsClient, CancellationToken stoppingToken)
    {
        var batch = resolutions.Select(res => new SendMessageBatchRequestEntry
        {
            Id = res.Height.ToString(),
            MessageBody = JsonSerializer.Serialize(new ProcessingStatusMessage
            {
                VideoId = videoId,
                Status = status,
                Files = 0,
                Height = res.Height,
                ResolutionProgress = 0
            }),
        }).ToList();

        await sqsClient.SendMessageBatchAsync(_processingStatusQueueOptions.Url.ToString(), batch, stoppingToken);
    }
}
