using Gateway.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Gateway.Integration.Tests.Infrastructure;

/// <summary>
/// Hands out contexts built from one fixed set of options. Deliberately not the pooled factory:
/// options there are shared across every context the pool serves, so a capturing interceptor would
/// collect commands from concurrent tests into the same list.
/// </summary>
internal sealed class TestDbContextFactory(DbContextOptions<MetricsReadDbContext> options)
    : IDbContextFactory<MetricsReadDbContext>
{
    /// <inheritdoc />
    public MetricsReadDbContext CreateDbContext() => new(options);
}
