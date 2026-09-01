using Gateway.Application.Interfaces;
using Gateway.Infrastructure.Persistence;
using Gateway.Infrastructure.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gateway.Infrastructure;

public static class DependencyInjectionRegistration
{
    public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        AddPersistence(services, configuration);
        AddQueryServices(services);
    }

    private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString("MetricsDb");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'MetricsDb' is not configured.");
        }

        // Pooled factory rather than a scoped context: GraphQL resolves fields in parallel, and a
        // read-only context has no shared change tracker to protect. Note that Phase 3's
        // RegisterDbContextFactory does not create the factory - this call is what does.
        services.AddPooledDbContextFactory<MetricsReadDbContext>(options => options
            .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure())
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTrackingWithIdentityResolution));
    }

    private static void AddQueryServices(IServiceCollection services)
    {
        services.AddMemoryCache();

        // Singleton, not scoped: the service holds no state and no context of its own - it creates
        // and disposes one per call from the (singleton) factory. That makes it safe to call from
        // parallel resolvers and removes any question about which scope HotChocolate resolves it
        // from.
        services.AddSingleton<IMetricReadingQueryService, MetricReadingQueryService>();
    }
}
