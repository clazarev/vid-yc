using Microsoft.Extensions.Hosting;

namespace Transcoder.Common;

public abstract class WorkerBase(IHostApplicationLifetime applicationLifetime, Serilog.ILogger logger, string name) : BackgroundService
{
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
        _ = applicationLifetime.ApplicationStopping.Register(() => logger.Information($"{name} worker is shutting down..."));
        // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
        _ = applicationLifetime.ApplicationStarted.Register(() => logger.Information($"{name} worker started and listening for messages..."));
        return base.StartAsync(cancellationToken);
    }

    protected void CleanUp(string workingDirectory)
    {
        var dirs = Directory.GetDirectories(workingDirectory);

        try
        {
            foreach (var dir in dirs)
            {
                Directory.Delete(dir, true);
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to cleanup up directories");
        }
    }
}
