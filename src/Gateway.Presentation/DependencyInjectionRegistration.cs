using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gateway.Presentation;

public static class DependencyInjectionRegistration
{
    public static void AddPresentation(this IServiceCollection services, IConfiguration configuration)
    {
        AddGraphQl(services, configuration);
    }

    private static void AddGraphQl(IServiceCollection services, IConfiguration configuration)
    {
        // Phase 3: AddGraphQLServer() lives here, not in Program.cs, so the host stays free of schema wiring.
        // Also owns the CORS policy driven by Cors:AllowedOrigins.
    }
}
