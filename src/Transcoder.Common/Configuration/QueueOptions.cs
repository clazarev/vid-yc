namespace Transcoder.Common.Configuration;

public class QueueOptions
{
    /// <summary>
    ///     Queue for initial video processing
    /// </summary>
    public const string VideoQueue = nameof(VideoQueue);

    /// <summary>
    ///     Queue for status of video processing
    /// </summary>
    public const string StatusQueue = nameof(StatusQueue);

    /// <summary>
    ///     Queue for chunk processing
    /// </summary>
    public const string ChunkQueue = nameof(ChunkQueue);

    /// <summary>
    ///     Queue for processed chunks
    /// </summary>
    public const string StreamQueue = nameof(StreamQueue);

    /// <summary>
    ///     Queue for processed chunks
    /// </summary>
    public const string ProcessedChunksQueue = nameof(ProcessedChunksQueue);

    public string? Url { get; init; }

    public int WaitTimeSeconds { get; set; }

    public int MaxNumberOfMessages { get; set; } = 1;
    public int VisibilityTimeoutSeconds { get; set; } = 30;
    public int MaxApproximateReceiveCount { get; set; } = 5;
}
