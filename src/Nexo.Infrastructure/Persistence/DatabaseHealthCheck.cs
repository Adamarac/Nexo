using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Nexo.Infrastructure.Persistence;

internal sealed class DatabaseHealthCheck(NexoDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await db.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Não foi possível conectar ao PostgreSQL.");
}
