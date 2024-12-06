namespace Transcoder.Common.MessageModels;

public record ProcessingStatusMessage
{
    public VideoStatus Status { get; set; }
    public Guid VideoId { get; set; }
    public int Files { get; set; }
    public int Height { get; set; }
    public int ResolutionProgress { get; set; }
}
