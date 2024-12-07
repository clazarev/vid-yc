using System.Collections.ObjectModel;

namespace Transcoder.Common.MessageModels;

public record ChunksInfoMessage
{
    public int Total { get; set; }
    public int ChunkNumber { get; set; }
    public required string ChunkName { get; set; }
    public string? Playlist { get; set; }
    public Guid VideoId { get; set; }
    public required ReadOnlyCollection<Resolution> Resolutions { get; set; }
    public double Duration { get; set; }
    public required string Key { get; set; }
    public string? AudioKey { get; set; }
}
