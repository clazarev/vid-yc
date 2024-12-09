using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using AutoMapper;
using Transcoder.ChunkSaver.Application.Interfaces;
using Transcoder.Common;
using Transcoder.Common.Configuration;
using Transcoder.Common.Entities;
using Transcoder.Common.MessageModels;
using Microsoft.Extensions.Options;
using Serilog.Context;
using Serilog.Events;
using SerilogTimings.Extensions;

namespace Transcoder.ChunkSaver;

#pragma warning disable CA1812
internal sealed class Worker(
    IHostApplicationLifetime applicationLifetime,
    Serilog.ILogger logger,
    IMapper mapper,
    IServiceProvider services,
    IAmazonSQS sqsClient,
    IOptionsMonitor<QueueOptions> queueOptions,
    StatusSender statusSender) : WorkerBase(applicationLifetime, logger, "transcoder-chunk-saver")
{
#pragma warning restore CA1812

    private readonly QueueOptions _processedQueueOptions = queueOptions.Get(QueueOptions.ProcessedChunksQueue);
    private readonly QueueOptions _streamQueueOptions = queueOptions.Get(QueueOptions.StreamQueue);
    private readonly Serilog.ILogger _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var request = new ReceiveMessageRequest
        {
            QueueUrl = _processedQueueOptions.Url.ToString(),
            MaxNumberOfMessages = _processedQueueOptions.MaxNumberOfMessages,
            WaitTimeSeconds = _processedQueueOptions.WaitTimeSeconds,
            VisibilityTimeout = _processedQueueOptions.VisibilityTimeoutSeconds
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            ReceiveMessageResponse response;

            using (var op = _logger.OperationAt(LogEventLevel.Debug, LogEventLevel.Debug).Begin("Receive message from processed chunks queue"))
            {
                response = await sqsClient.ReceiveMessageAsync(request, stoppingToken);
                if (response.Messages.Count == 0)
                {
                    op.Cancel();
                    continue;
                }

                op.Complete("NumberOfMessages", response.Messages.Count);
            }

            using var scope = services.CreateScope();
            var chunksRepository = scope.ServiceProvider.GetRequiredService<IChunkRepository>();

            await ProcessMessages(response.Messages, chunksRepository, stoppingToken);
        }
    }

    private async Task ProcessMessages(List<Message> messages, IChunkRepository chunksRepository, CancellationToken stoppingToken)
    {
        foreach (var sqsMessage in messages)
        {
            var message = JsonSerializer.Deserialize<ProcessedChunkMessage>(sqsMessage.Body)!;

            using var _ = LogContext.PushProperty("VideoId", message.VideoId);

            await chunksRepository.AddAsync(mapper.Map<ProcessedChunk>(message)!, stoppingToken);

            var processedChunksCount = await chunksRepository.GetTotalAsync(message.VideoId, message.Height, stoppingToken);

            var resolutionProgress = 100 * processedChunksCount / message.Total;

            await statusSender.SendStatus(message.VideoId, VideoStatus.Processing, sqsClient, stoppingToken, message.Height, resolutionProgress);

            var isCancellationRequested = sqsMessage.IsCancellationRequestMessage();

            // cancelling the process and marking the video as failed
            if (isCancellationRequested)
            {
                _logger.Warning("Received cancellation request message on chunk {@Chunk}. Cancelling chunk-saver task...", message);
                await statusSender.SendStatus(message.VideoId, VideoStatus.Rejected, sqsClient, stoppingToken);

                await sqsClient.DeleteMessageAsync(_processedQueueOptions.Url.ToString(), sqsMessage.ReceiptHandle, stoppingToken);
                continue;
            }

            if (resolutionProgress < 100)
            {
                _logger.Information("{ChunkNumber} processed out of {Total}",
                    processedChunksCount, message.Total);

                await sqsClient.DeleteMessageAsync(_processedQueueOptions.Url.ToString(), sqsMessage.ReceiptHandle, stoppingToken);
                continue;
            }

            await statusSender.SendStatus(message.VideoId, VideoStatus.Processed, sqsClient, stoppingToken, message.Height, resolutionProgress);

            _logger.Information("All chunks for {Height}:{Progress} are ready, sending to stream queue...", message.Height, resolutionProgress);

            var streamRequest = new SendMessageRequest
            {
                MessageBody = JsonSerializer.Serialize(new CollectChunksMessage
                {
                    VideoId = message.VideoId,
                    Playlist = message.Playlist,
                    Height = message.Height
                }),
                QueueUrl = _streamQueueOptions.Url.ToString(),
                MessageGroupId = message.VideoId.ToString()
            };

            await sqsClient.SendMessageAsync(streamRequest, stoppingToken);

            await sqsClient.DeleteMessageAsync(_processedQueueOptions.Url.ToString(), sqsMessage.ReceiptHandle, stoppingToken);
        }
    }
}
