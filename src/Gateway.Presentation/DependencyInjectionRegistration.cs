using Gateway.Infrastructure.Persistence;
using Gateway.Presentation.Errors;
using Gateway.Presentation.Health;
using Gateway.Presentation.Queries;
using Gateway.Presentation.Types;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Gateway.Presentation;

public static class DependencyInjectionRegistration
{
    private const int DefaultPageSize = 20;

    private const int MaxPageSize = 100;

    private const int MaxCost = 5_000;

    private const int MaxExecutionDepth = 8;

    private static readonly TimeSpan ExecutionTimeout = TimeSpan.FromSeconds(30);

    public static void AddPresentation(this IServiceCollection services, IConfiguration configuration)
    {
        AddCorsPolicy(services, configuration);
        AddHealthChecks(services);
        AddGraphQl(services, configuration);
    }

    private static void AddGraphQl(IServiceCollection services, IConfiguration configuration)
    {
        var builder = services
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
            .AddType<MetricAggregationInputType>()
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
            .AddMaxExecutionDepthRule(MaxExecutionDepth)
            .ModifyRequestOptions(options => options.ExecutionTimeout = ExecutionTimeout)

            // Constructed by hand rather than resolved: error filters are activated from
            // HotChocolate's schema service provider, a separate container with no logging and no
            // hosting services registered. Constructor injection there fails when the executor is
            // built. GetRootServiceProvider bridges back to the application container.
            .AddErrorFilter(schemaServices =>
            {
                IServiceProvider app = schemaServices.GetRootServiceProvider();

                return new GraphQlErrorFilter(
                    app.GetRequiredService<ILogger<GraphQlErrorFilter>>(),
                    app.GetRequiredService<IHostEnvironment>());
            });

        // HotChocolate disables introspection outside Development by default, which is the right
        // default for a public API and the wrong one for a demo whose frontend needs codegen. This
        // opt-in re-enables it without pretending the deployment is a development environment.
        // Left unset, the environment-based default stands - so Nitro still works locally.
        if (configuration.GetValue<bool>("GraphQL:AllowIntrospection"))
        {
            builder.DisableIntrospection(false);
        }
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
