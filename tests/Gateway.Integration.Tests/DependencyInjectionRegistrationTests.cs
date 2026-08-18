using Gateway.Application;
using Gateway.Infrastructure;
using Gateway.Presentation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gateway.Integration.Tests;

public class DependencyInjectionRegistrationTests
{
    private const string ConnectionStringKey = "ConnectionStrings:MetricsDb";

    private const string FakeConnectionString =
        "Server=(local);Database=GatewayCompositionTest;Trusted_Connection=True;TrustServerCertificate=True";

    [Fact]
    public void AllLayersComposeAndServiceProviderBuilds()
    {
        IConfiguration configuration = BuildConfiguration(FakeConnectionString);
        IServiceCollection services = new ServiceCollection();

        services.AddApplication(configuration);
        services.AddInfrastructure(configuration);
        services.AddPresentation(configuration);

        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);

        Assert.NotNull(provider);
    }

    [Fact]
    public void AddInfrastructureThrowsWhenTheConnectionStringIsMissing()
    {
        IConfiguration configuration = BuildConfiguration(connectionString: null);
        IServiceCollection services = new ServiceCollection();

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(() => services.AddInfrastructure(configuration));

        Assert.Contains("MetricsDb", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddInfrastructureThrowsWhenTheConnectionStringIsBlank()
    {
        // appsettings.Development.json ships the key as an empty string, so blank has to fail
        // with the same clear message rather than a downstream SqlClient error.
        IConfiguration configuration = BuildConfiguration(string.Empty);
        IServiceCollection services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() => services.AddInfrastructure(configuration));
    }

    private static IConfiguration BuildConfiguration(string? connectionString)
    {
        Dictionary<string, string?> values = [];

        if (connectionString is not null)
        {
            values[ConnectionStringKey] = connectionString;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
