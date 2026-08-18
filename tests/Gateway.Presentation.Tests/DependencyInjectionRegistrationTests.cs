using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gateway.Presentation.Tests;

public class DependencyInjectionRegistrationTests
{
    [Fact]
    public void AddPresentationRegistersWithoutThrowing()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();
        IServiceCollection services = new ServiceCollection();

        services.AddPresentation(configuration);

        Assert.NotNull(services);
    }
}
