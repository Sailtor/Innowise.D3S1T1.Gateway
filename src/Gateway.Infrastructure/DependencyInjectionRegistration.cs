using Gateway.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gateway.Infrastructure;

public static class DependencyInjectionRegistration
{
    public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        AddPersistence(services, configuration);
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
}
