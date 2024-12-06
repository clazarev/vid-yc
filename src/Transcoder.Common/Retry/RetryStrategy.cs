namespace Transcoder.Common.Retry;

public class RetryStrategy(int maxRetries, TimeSpan interval)
{
    private int MaxRetries { get; set; } = maxRetries;

    private TimeSpan Interval { get; set; } = interval;

    public int getMaxRetries()
    {
        return MaxRetries;
    }

    public TimeSpan getTimeInterval()
    {
        return Interval;
    }
}
