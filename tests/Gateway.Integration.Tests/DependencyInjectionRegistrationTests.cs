using Gateway.Application;
using Gateway.Infrastructure;
using Gateway.Presentation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gateway.Integration.Tests;

public class DependencyInjectionRegistrationTests
{
    [Fact]
    public void AllLayersComposeAndServiceProviderBuilds()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();
        IServiceCollection services = new ServiceCollection();

        services.AddApplication(configuration);
        services.AddInfrastructure(configuration);
        services.AddPresentation(configuration);

        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);

        Assert.NotNull(provider);
    }
}
