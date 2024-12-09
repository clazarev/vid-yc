using Transcoder.API.Application.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Transcoder.API.HealthChecks;

internal class DatabaseHealthCheck(IApplicationDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var canConnect = await dbContext.CanConnectAsync(cancellationToken);
        return canConnect ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
    }
}
