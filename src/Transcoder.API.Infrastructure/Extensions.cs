using VideoStatus = Transcoder.Common.MessageModels.VideoStatus;
using VideoStatusGrpc = Transcoder.API.Infrastructure.gRPC.VideoStatus;

namespace Transcoder.API.Infrastructure;

public static class Extensions
{
    public static VideoStatusGrpc ToGetVideoResponseStatus(this VideoStatus status) => status switch
    {
        VideoStatus.Added => VideoStatusGrpc.Added,
        VideoStatus.Processing => VideoStatusGrpc.Processing,
        VideoStatus.Verified => VideoStatusGrpc.Verified,
        VideoStatus.Chopped => VideoStatusGrpc.Chopped,
        VideoStatus.Processed => VideoStatusGrpc.Processed,
        VideoStatus.Gluing => VideoStatusGrpc.Gluing,
        VideoStatus.Done => VideoStatusGrpc.Done,
        VideoStatus.Rejected => VideoStatusGrpc.Rejected,
        _ => VideoStatusGrpc.Rejected // we don't want to fail
    };
}
