using System.Globalization;
using System.Text.Json;
using Gateway.Application;
using Gateway.Infrastructure;
using Gateway.Integration.Tests.Infrastructure;
using Gateway.Presentation;
using HotChocolate;
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
    public async Task BucketsTheWindowInUtcRegardlessOfTheHostTimeZone()
    {
        // The only place in the suite where `from`/`to` arrive through the GraphQL DateTime scalar
        // rather than as CLR DateTimes. That scalar coerces to DateTimeOffset and the type
        // converter then narrows it to the DateTime? on the input model - so if the narrowing ever
        // went through local time, this window would shift by the host's offset and the 10:00-12:00
        // rows would fall outside an 18 August window on any machine that is not UTC. It would
        // pass on a UTC CI runner and fail on a developer's laptop, which is the worst way for a
        // test to be wrong.
        const string Query = """
            {
              metricAggregation(
                input: {
                  field: CO2
                  interval: HOUR
                  groupByRoom: true
                  from: "2026-08-18T00:00:00Z"
                  to: "2026-08-19T00:00:00Z"
                }
              ) {
                room
                bucketStart
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

        Assert.True(
            operationResult.Errors is null or { Count: 0 },
            string.Join(Environment.NewLine, operationResult.Errors?.Select(e => e.Message) ?? []));

        using JsonDocument document = JsonDocument.Parse(operationResult.ToJson());
        JsonElement buckets = document.RootElement.GetProperty("data").GetProperty("metricAggregation");

        // kitchen has air quality at 10:00, 10:30 and 11:30; office has one at 10:00. Ordered by
        // room then bucket, that is three buckets and the first starts at 10:00 UTC.
        Assert.Equal(3, buckets.GetArrayLength());

        // Parsed rather than string-matched: the assertion is about the instant, not about how the
        // scalar chooses to format it.
        string bucketStart = buckets[0].GetProperty("bucketStart").GetString()!;

        Assert.Equal(
            new DateTimeOffset(2026, 8, 18, 10, 0, 0, TimeSpan.Zero),
            DateTimeOffset.Parse(bucketStart, CultureInfo.InvariantCulture));

        Assert.Equal(2, buckets[0].GetProperty("stats").GetProperty("count").GetInt32());
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
