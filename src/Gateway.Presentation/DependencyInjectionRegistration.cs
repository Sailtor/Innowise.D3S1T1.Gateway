using Gateway.Infrastructure.Persistence;
using Gateway.Presentation.Health;
using Gateway.Presentation.Queries;
using Gateway.Presentation.Types;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gateway.Presentation;

public static class DependencyInjectionRegistration
{
    private const int DefaultPageSize = 20;

    private const int MaxPageSize = 100;

    private const int MaxCost = 5_000;

    private const int MaxExecutionDepth = 8;

    public static void AddPresentation(this IServiceCollection services, IConfiguration configuration)
    {
        AddCorsPolicy(services, configuration);
        AddHealthChecks(services);
        AddGraphQl(services);
    }

    private static void AddGraphQl(IServiceCollection services)
    {
        services
            .AddGraphQLServer()

            // Hands each resolver its own pooled context and disposes it afterwards. Note this does
            // NOT create the factory - AddPooledDbContextFactory in AddInfrastructure does that.
            .RegisterDbContextFactory<MetricsReadDbContext>()

            // The root operation type is named after the CLR class, so MetricsQuery would
            // surface as `type MetricsQuery` and `schema { query: MetricsQuery }`. Every client
            // and codegen tool expects `Query`; the name is pinned here rather than dictated by
            // what the class happens to be called.
            .AddQueryType<MetricsQuery>(d => d.Name(OperationTypeNames.Query))

            // The three implementations must be registered explicitly: nothing in the schema
            // references them by name, so type discovery would never find them and __typename
            // would have nothing to resolve to.
            .AddType<MetricReadingInterfaceType>()
            .AddType<EnergyReadingType>()
            .AddType<AirQualityReadingType>()
            .AddType<MotionReadingType>()
            .AddType<MetricReadingTypeEnumType>()
            .AddFiltering()
            .AddSorting()

            // Creates activities only; Phase 5 adds the OpenTelemetry exporter that reads them.
            .AddInstrumentation()
            .ModifyPagingOptions(options =>
            {
                options.DefaultPageSize = DefaultPageSize;
                options.MaxPageSize = MaxPageSize;
                options.IncludeTotalCount = true;
            })

            // Defaults are 1_000 each. Raised because aggregation fields in Phase 4 legitimately
            // cost more than a simple entity read, not because the limits were inconvenient.
            .ModifyCostOptions(options =>
            {
                options.MaxFieldCost = MaxCost;
                options.MaxTypeCost = MaxCost;
            })
            .AddMaxExecutionDepthRule(MaxExecutionDepth);
    }

    private static void AddCorsPolicy(IServiceCollection services, IConfiguration configuration)
    {
        // Empty by default: the frontend origin is deployment configuration, not a source-code
        // constant, and a wide-open default is not something to inherit by accident.
        string[] allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        services.AddCors(options => options.AddDefaultPolicy(policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()));
    }

    private static void AddHealthChecks(IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<MetricsDatabaseHealthCheck>("metrics-db");
    }
}
