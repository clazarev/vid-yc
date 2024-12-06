namespace Transcoder.Common.MessageModels;

public record ProcessedChunkMessage
{
    public int Total { get; set; }
    public int ChunkNumber { get; set; }
    public string? ChunkName { get; set; }
    public string? Playlist { get; set; }
    public Guid VideoId { get; set; }
    public required string Key { get; set; }
    public string? AudioKey { get; set; }
    public int Height { get; set; }
}
