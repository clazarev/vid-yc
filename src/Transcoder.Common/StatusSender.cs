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
            QueueUrl = _processingStatusQueueOptions.Url,
        };
        await sqsClient.SendMessageAsync(sendRequest, stoppingToken);
    }

    public async Task SendResolutionBatchStatus(Guid videoId, VideoStatus status, List<Resolution> resolutions, IAmazonSQS sqsClient, CancellationToken stoppingToken)
    {
        List<SendMessageBatchRequestEntry> batch = [];
        foreach (var resolution in resolutions)
        {
            var sendRequest = new SendMessageBatchRequestEntry
            {
                Id = resolution.Height.ToString(),
                MessageBody = JsonSerializer.Serialize(new ProcessingStatusMessage
                {
                    VideoId = videoId,
                    Status = status,
                    Files = 0,
                    Height = resolution.Height,
                    ResolutionProgress = 0
                }),
            };
            batch.Add(sendRequest);
        }

        await sqsClient.SendMessageBatchAsync(_processingStatusQueueOptions.Url, batch, stoppingToken);
    }
}
