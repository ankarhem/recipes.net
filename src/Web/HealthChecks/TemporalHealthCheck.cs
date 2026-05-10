using Microsoft.Extensions.Diagnostics.HealthChecks;
using Temporalio.Client;

namespace Web.HealthChecks;

public sealed class TemporalHealthCheck(ITemporalClient client, ILogger<TemporalHealthCheck> logger)
    : IHealthCheck
{
    private readonly ILogger<TemporalHealthCheck> _logger = logger;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var isHealthy = await client.Connection.CheckHealthAsync(
                client.Connection.WorkflowService,
                new RpcOptions { CancellationToken = cancellationToken }
            );

            if (isHealthy)
            {
                return HealthCheckResult.Healthy("Temporal server is reachable");
            }

            _logger.LogWarning("Temporal health check reported unhealthy");
            return HealthCheckResult.Unhealthy("Temporal server is unreachable");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Temporal health check failed");
            return HealthCheckResult.Unhealthy("Temporal server is unreachable", ex);
        }
    }
}
