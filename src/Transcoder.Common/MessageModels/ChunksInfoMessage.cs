namespace Transcoder.Common.MessageModels;

public record ChunksInfoMessage
{
    public int Total { get; set; }
    public int ChunkNumber { get; set; }
    public required string ChunkName { get; set; }
    public string? Playlist { get; set; }
    public Guid VideoId { get; set; }
    public List<Resolution> Resolutions { get; set; } = [];
    public double Duration { get; set; }
    public required string Key { get; set; }
    public string? AudioKey { get; set; }
}
