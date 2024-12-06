using Microsoft.Extensions.Logging;

namespace Transcoder.Common.Retry;

public class RetryExecutor(ILogger<RetryExecutor> logger, RetryStrategy retryStrategy)
{
    public bool Retry(Action logic)
    {
        int retries = 0;
        int maxRetries = retryStrategy.getMaxRetries();
        TimeSpan interval = retryStrategy.getTimeInterval();

        while (true)
        {
            try
            {
                retries++;
                logic();
                return true;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Retry {Retry} executing logic", retries);

                if (retries == maxRetries)
                {
                    return false;
                }

                Task.Delay(interval).Wait();
            }
        }
    }
}
