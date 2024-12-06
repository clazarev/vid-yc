namespace Transcoder.Common;

public static class PlaylistExtensions
{
    public const string PlaylistExtSearchPattern = "*.m3u8";
    public const string SegmentFileExtSearchPattern = "*.ts";
    public static int GetPlaylistFilesCount(string playlistDirectory)
    {
        return Directory.EnumerateFiles(playlistDirectory)
            .Union(Directory.EnumerateFiles(playlistDirectory, PlaylistExtSearchPattern, SearchOption.TopDirectoryOnly))
            .Union(Directory.EnumerateFiles(playlistDirectory, SegmentFileExtSearchPattern, SearchOption.TopDirectoryOnly)).Count();

    }
}
