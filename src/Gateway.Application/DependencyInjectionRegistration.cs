using FluentValidation;
using Gateway.Application.Validation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gateway.Application;

public static class DependencyInjectionRegistration
{
    public static void AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        // Singleton, not the scanner's Scoped default. MetricReadingQueryService is a singleton, so
        // a scoped validator would be a captive dependency and BuildServiceProvider(validateScopes)
        // would reject it. Validators are stateless, so singleton is safe as well as necessary.
        services.AddValidatorsFromAssemblyContaining<MetricAggregationQueryValidator>(
            ServiceLifetime.Singleton);
    }
}
