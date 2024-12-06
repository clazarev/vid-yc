namespace Transcoder.Common.MessageModels;

public record VideoMessage
{
    public Guid VideoId { get; set; }
    public string? FileUrl { get; set; }
    public string? Playlist { get; set; }
}
