using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gateway.Application;

public static class DependencyInjectionRegistration
{
    public static void AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        // Intentionally empty. The Application layer is pure - interfaces, models and the
        // aggregation validator, none of which need registering. IMetricReadingQueryService
        // is implemented in Infrastructure and registered there, next to the DbContext factory
        // it depends on.
    }
}
