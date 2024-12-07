namespace Transcoder.Common.Retry;

public class RetryStrategy
{
    public int MaxRetries { get; set; }

    public TimeSpan TimeInterval
    {
        get;
        set;
    }
}
