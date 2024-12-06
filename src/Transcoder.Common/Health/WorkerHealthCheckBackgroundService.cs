using Microsoft.Extensions.Hosting;

namespace Transcoder.Common.Health;

public class WorkerHealthCheckBackgroundService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            Touch("/tmp/healthy");
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private static void Touch(string path)
    {
        using var fileStream = File.Open(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
    }
}
