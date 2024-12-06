using Grpc.Core;
using Transcoder.API.Application.Interfaces;
using Transcoder.API.Infrastructure.gRPC;
using Microsoft.Extensions.Logging;

namespace Transcoder.API.Infrastructure;

public class VideoService(ILogger<VideoService> logger, IVideoService videoService) : VideosProcessing.VideosProcessingBase
{
    public override async Task<StartVideoProcessingResponse?> StartVideoProcessing(StartVideoProcessingRequest request, ServerCallContext context)
    {
        logger.LogInformation("RPC StartVideo {VideoId}", request.VideoId);
        if (!Uri.TryCreate(request.FileUrl, UriKind.Absolute, out _))
        {
            var ex = new RpcException(new Status(StatusCode.InvalidArgument, $"{nameof(request.FileUrl)} must be a valid URI"));
            logger.LogError(ex, "RPC StartVideo {VideoId} failed", request.VideoId);
            throw ex;
        }

        var result = await videoService.AddForProcessingAsync(Guid.Parse(request.VideoId), request.FileUrl, request.Playlist, context.CancellationToken);
        return new StartVideoProcessingResponse
        {
            VideoId = result.Id.ToString(),
            Status = result.Status.ToGetVideoResponseStatus(),
            Duration = (int)result.Duration,
            Codec = result.Codec,
            Format = result.Format
        };
    }

    public override async Task<GetVideoResponse?> GetVideo(GetVideoRequest request, ServerCallContext context)
    {
        logger.LogDebug("RPC GetVideo {VideoId}", request.VideoId);
        var result = await videoService.GetAsync(Guid.Parse(request.VideoId), context.CancellationToken);
        if (result is null)
        {
            var ex = new RpcException(new Status(StatusCode.NotFound, $"Video Id {request.VideoId} not found"));
            logger.LogError(ex, "RPC GetVideo {VideoId} failed", request.VideoId);
            throw ex;
        }

        return new GetVideoResponse
        {
            VideoId = result.Id.ToString(),
            Codec = result.Codec,
            Status = result.Status.ToGetVideoResponseStatus(),
            Duration = (int)result.Duration,
            Format = result.Format,
            Progress = result.Progress
        };
    }
}
