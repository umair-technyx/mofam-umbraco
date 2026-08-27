using Microsoft.Extensions.Diagnostics.HealthChecks;
using Mofam.Infrastructure.Abstractions;

namespace Mofam.Infrastructure.HealthChecks;

public sealed class DatabaseHealthCheck(IDatabaseConnectivityService databaseConnectivityService) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var result = databaseConnectivityService.CanConnect()
            ? HealthCheckResult.Healthy("Database connection is healthy.")
            : HealthCheckResult.Unhealthy("Unable to connect to the database.");

        return Task.FromResult(result);
    }
}
