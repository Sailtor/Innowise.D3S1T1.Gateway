using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gateway.Application;

public static class DependencyInjectionRegistration
{
    public static void AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        // Phase 4: register IMetricReadingQueryService and the aggregation input validators.
    }
}
