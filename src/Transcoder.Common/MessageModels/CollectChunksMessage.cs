namespace Transcoder.Common.MessageModels
{
    public record CollectChunksMessage
    {
        public Guid VideoId { get; set; }
        public string? Playlist { get; set; }
        public int Height { get; set; }
    }
}
