using Gateway.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Gateway.Presentation.Health;

/// <summary>
/// Reports on the database this service reads but does not own.
/// <para>
/// DataProcessor creates and migrates the schema, and migrations do not run outside Development,
/// so the table can legitimately be absent when the Gateway starts. That must surface as Unhealthy,
/// not as a crash - a service that comes up and reports a recoverable problem is worth far more
/// than one that crash-loops. Hence the probe against the table, not just CanConnect.
/// </para>
/// </summary>
internal sealed class MetricsDatabaseHealthCheck(IDbContextFactory<MetricsReadDbContext> contextFactory)
    : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using MetricsReadDbContext dbContext =
                await contextFactory.CreateDbContextAsync(cancellationToken);

            if (!await dbContext.Database.CanConnectAsync(cancellationToken))
            {
                return HealthCheckResult.Unhealthy("Cannot connect to the metrics database.");
            }

            await dbContext.MetricReadings.Take(1).AnyAsync(cancellationToken);

            return HealthCheckResult.Healthy("Metrics database is reachable and queryable.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "The metrics database is not queryable. The schema may not have been provisioned yet.",
                exception);
        }
    }
}
