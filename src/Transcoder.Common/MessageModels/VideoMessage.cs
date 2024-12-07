namespace Transcoder.Common.MessageModels;

public record VideoMessage
{
    public required Guid VideoId { get; set; }
    public required Uri FileUrl { get; set; }
    public required string Playlist { get; set; }
}
