using Gateway.Application;
using Gateway.Infrastructure;
using Gateway.Integration.Tests.Infrastructure;
using Gateway.Presentation;
using HotChocolate.Execution;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Gateway.Integration.Tests;

/// <summary>
/// The whole stack end to end: a GraphQL document in, real SQL out, and the error filter in the
/// middle - against a real SQL Server.
/// <para>
/// Deliberately not WebApplicationFactory. It runs the entry point and aborts it once the host is
/// built, and Program.cs closes the static Serilog logger in a finally block, so the second test
/// would get a disposed logger and fail with "the entry point exited without ever building an
/// IHost". Executing through the request executor covers everything except the HTTP transport and
/// avoids that entirely.
/// </para>
/// </summary>
[Collection(SqlServerCollection.Name)]
public class GraphQlExecutionTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task ServesThePagedReadingsFeed()
    {
        const string Query = """
            {
              metricReadings(take: 5, order: [{ receivedAt: DESC }]) {
                totalCount
                items { id room type receivedAt }
              }
            }
            """;

        await AssertNoErrorsAsync(Query);
    }

    [Fact]
    public async Task ResolvesTheConcreteTypeBehindTheInterface()
    {
        // Proves the interface, its three implementations and the TPH discriminator all line up at
        // execution time, not merely at schema-build time.
        const string Query = """
            {
              latestReadings {
                room
                type
                ... on EnergyReading { energyAmount }
                ... on AirQualityReading { co2 pm25 humidity }
                ... on MotionReading { isMotionDetected }
              }
            }
            """;

        await AssertNoErrorsAsync(Query);
    }

    [Fact]
    public async Task ServesTheAggregationAndDashboardFields()
    {
        const string Query = """
            {
              availableRooms
              rooms {
                room
                totalReadings
                latestReading { type receivedAt }
              }
              metricAggregation(input: { field: CO2, groupByRoom: true }) {
                room
                stats { count min max average sum }
              }
            }
            """;

        await AssertNoErrorsAsync(Query);
    }

    [Fact]
    public async Task ReturnsValidationFailuresWithACodeAndTheOffendingField()
    {
        // A MINUTE interval with no window is the bucket explosion the validator rejects. This is
        // the full path: FluentValidation throws, the error filter fans the failures out, and each
        // one arrives with a stable code and a field a frontend can attach the message to.
        const string Query = """
            {
              metricAggregation(input: { field: CO2, interval: MINUTE }) {
                stats { count }
              }
            }
            """;

        await using ServiceProvider provider = BuildProvider();
        IRequestExecutor executor = await provider.GetRequestExecutorAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        await using IExecutionResult result = await executor.ExecuteAsync(
            Query, TestContext.Current.CancellationToken);

        OperationResult operationResult = Assert.IsType<OperationResult>(result);

        Assert.NotNull(operationResult.Errors);
        Assert.NotEmpty(operationResult.Errors);
        Assert.All(operationResult.Errors, error => Assert.Equal("VALIDATION_FAILED", error.Code));
        Assert.All(operationResult.Errors, error => Assert.NotNull(error.Extensions?["field"]));
    }

    private async Task AssertNoErrorsAsync(string query)
    {
        await using ServiceProvider provider = BuildProvider();
        IRequestExecutor executor = await provider.GetRequestExecutorAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        await using IExecutionResult result = await executor.ExecuteAsync(
            query, TestContext.Current.CancellationToken);

        OperationResult operationResult = Assert.IsType<OperationResult>(result);

        // Report the messages rather than just a count: a failure here is usually an EF
        // translation problem, and the message is the whole diagnosis.
        Assert.True(
            operationResult.Errors is null or { Count: 0 },
            string.Join(Environment.NewLine, operationResult.Errors?.Select(e => e.Message) ?? []));

        Assert.NotNull(operationResult.Data);
    }

    private ServiceProvider BuildProvider()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:MetricsDb"] = fixture.ConnectionString,
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        // WebApplicationBuilder would contribute these; a bare ServiceCollection does not, and the
        // error filter resolves both from the application container when the executor is built.
        services.AddLogging();

        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Production);
        services.AddSingleton(environment);

        services.AddApplication(configuration);
        services.AddInfrastructure(configuration);
        services.AddPresentation(configuration);

        return services.BuildServiceProvider();
    }
}
