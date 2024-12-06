using FFMpegCore.Arguments;

namespace Transcoder.Composer.Application.Services;
public class HlsArguments : IArgument
{
    // -hls_segment_filename {Height}_%04d.ts
    public string Text => "-codec: copy -hls_time 10 -hls_list_size 0 -hls_playlist_type vod -f hls"; // default way
}
