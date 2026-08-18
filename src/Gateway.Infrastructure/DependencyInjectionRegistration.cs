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
        // Phase 2: AddPooledDbContextFactory<MetricsReadDbContext> against ConnectionStrings:MetricsDb.
        // This service is a read-only consumer of the DataProcessor schema: no migrations, ever.
    }
}
