using Gateway.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gateway.Infrastructure.Persistence;

/// <summary>
/// Read-only view over the MetricReadings table owned by the DataProcessor service.
/// <para>
/// This context deliberately has no Migrations folder and never calls Database.Migrate():
/// DataProcessor is the sole schema owner, and the Gateway assumes the schema already exists.
/// SaveChanges is disabled so a later change cannot quietly turn this into a second writer.
/// </para>
/// </summary>
public class MetricsReadDbContext(DbContextOptions<MetricsReadDbContext> options) : DbContext(options)
{
    private const string ReadOnlyMessage =
        "MetricsReadDbContext is read-only. The MetricReadings schema and its data are owned by the DataProcessor service.";

    public DbSet<MetricReading> MetricReadings => Set<MetricReading>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
        => throw new InvalidOperationException(ReadOnlyMessage);

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException(ReadOnlyMessage);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(AssemblyReference.Assembly);
    }
}
