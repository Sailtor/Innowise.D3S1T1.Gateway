using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gateway.Application.Tests;

public class DependencyInjectionRegistrationTests
{
    [Fact]
    public void AddApplicationRegistersWithoutThrowing()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();
        IServiceCollection services = new ServiceCollection();

        services.AddApplication(configuration);

        Assert.NotNull(services);
    }
}
