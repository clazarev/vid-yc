using Microsoft.Extensions.Logging;

namespace Transcoder.Common.Retry;

public class RetryExecutor(ILogger<RetryExecutor> logger, RetryStrategy retryStrategy)
{
    public bool Retry(Action logic)
    {
        int retries = 0;
        int maxRetries = retryStrategy.MaxRetries;
        TimeSpan interval = retryStrategy.TimeInterval;

        while (true)
        {
            try
            {
                retries++;
                logic();
                return true;
            }
#pragma warning disable CA1031
            catch (Exception ex)
#pragma warning restore CA1031
            {
#pragma warning disable CA1848
                logger.LogWarning(ex, "Retry {Retry} executing logic", retries);
#pragma warning restore CA1848

                if (retries == maxRetries)
                {
                    return false;
                }

                Task.Delay(interval).Wait();
            }
        }
    }
}
