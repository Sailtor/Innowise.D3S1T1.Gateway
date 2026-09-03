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
/// Pins the exact JSON each dashboard query returns.
/// <para>
/// The executor tests next door assert that a query runs without errors and returns data. That
/// catches a broken query but not a changed one: a renamed field, a number that quietly became a
/// string, a nullable that started coming back null, or an ordering that drifted are all invisible
/// to them and all break a client. The SDL snapshot covers the shape of the contract; this covers
/// the shape of the payload, which is the half a frontend actually parses.
/// </para>
/// <para>
/// Every query here is fully ordered - by the resolver, not by luck - so the seed produces one
/// correct answer rather than one of several. Anything genuinely nondeterministic must not be
/// snapshotted; put it in an assertion instead. That is why none of these select `id`: the values
/// come from an IDENTITY column, so they encode insert order and would turn a reordered seed into
/// eight failing snapshots that say nothing about the schema.
/// </para>
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class ResponseSnapshotTests(SqlServerFixture fixture)
{
    /// <summary>
    /// Gets the queries to snapshot, keyed by the file each one is pinned in.
    /// </summary>
    public static TheoryData<string, string> Queries => new()
    {
        {
            "paged-readings",
            """
            {
              metricReadings(skip: 0, take: 3, order: [{ receivedAt: DESC }, { room: ASC }]) {
                totalCount
                pageInfo { hasNextPage hasPreviousPage }
                items {
                  room
                  type
                  receivedAt
                  ... on EnergyReading { energyAmount }
                  ... on AirQualityReading { co2 pm25 humidity }
                  ... on MotionReading { isMotionDetected }
                }
              }
            }
            """
        },
        {
            "filtered-readings",
            """
            {
              metricReadings(
                where: { room: { eq: "kitchen" }, type: { eq: AIR_QUALITY } }
                order: [{ receivedAt: ASC }]
              ) {
                totalCount
                items { room type receivedAt ... on AirQualityReading { co2 } }
              }
            }
            """
        },
        {
            "latest-readings",
            """
            {
              latestReadings {
                room
                type
                receivedAt
                ... on EnergyReading { energyAmount }
                ... on AirQualityReading { co2 }
                ... on MotionReading { isMotionDetected }
              }
            }
            """
        },
        {
            "rooms",
            """
            {
              availableRooms
              rooms {
                room
                totalReadings
                latestReading { type receivedAt }
                latestByType { type receivedAt }
              }
            }
            """
        },
        {
            "aggregation-ungrouped",
            """
            {
              metricAggregation(input: { field: CO2 }) {
                room
                bucketStart
                stats { count min max average sum }
              }
            }
            """
        },
        {
            "aggregation-by-room",
            """
            {
              metricAggregation(input: { field: CO2, groupByRoom: true }) {
                room
                bucketStart
                stats { count min max average sum }
              }
            }
            """
        },
        {
            "aggregation-by-hour",
            """
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
                stats { count average }
              }
            }
            """
        },
        {
            // Motion maps the boolean to 1/0, so sum is the detection count and average the rate.
            // Worth pinning: it is the one field whose numbers mean something other than they look.
            "aggregation-motion",
            """
            {
              metricAggregation(input: { field: MOTION_DETECTED, groupByRoom: true }) {
                room
                stats { count min max average sum }
              }
            }
            """
        },
    };

    [Theory]
    [MemberData(nameof(Queries))]
    public async Task ResponseMatchesTheCommittedSnapshot(string name, string query)
    {
        await using ServiceProvider provider = BuildProvider();
        IRequestExecutor executor = await provider.GetRequestExecutorAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        await using IExecutionResult result = await executor.ExecuteAsync(
            query, TestContext.Current.CancellationToken);

        OperationResult operationResult = Assert.IsType<OperationResult>(result);

        // A snapshot of an error response would pin the bug rather than the behaviour, and the
        // message is the diagnosis worth reading first.
        Assert.True(
            operationResult.Errors is null or { Count: 0 },
            string.Join(Environment.NewLine, operationResult.Errors?.Select(e => e.Message) ?? []));

        string actual = Normalise(operationResult.ToJson());

        // The output directory, not the source tree: ContinuousIntegrationBuild rewrites
        // [CallerFilePath] to a deterministic path that does not exist on disk. Same reasoning as
        // SchemaSnapshotTests, and the same reason the csproj copies Snapshots/ to the output.
        //
        // System.IO.Path spelled out: `using HotChocolate` (which ToJson needs) brings
        // HotChocolate.Path into scope, and a bare `Path` is then ambiguous. SchemaSnapshotTests
        // qualifies it for exactly this reason.
        string expectedPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Snapshots", $"{name}.json");
        string actualPath = expectedPath + ".actual";

        if (!File.Exists(expectedPath))
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(actualPath)!);
            await File.WriteAllTextAsync(actualPath, actual, TestContext.Current.CancellationToken);

            // A failure rather than a silent pass: a snapshot that becomes the baseline without
            // anyone reading it is not a test. The first version is the one worth reviewing.
            Assert.Fail(
                $"No snapshot exists for '{name}'. The current response was written to {actualPath}. "
                + $"Review it, copy it to Snapshots/{name}.json in the test project, commit it, and re-run.");
        }

        string expected = Normalise(
            await File.ReadAllTextAsync(expectedPath, TestContext.Current.CancellationToken));

        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            // Leave the new response on disk so the diff can be read rather than reconstructed.
            await File.WriteAllTextAsync(actualPath, actual, TestContext.Current.CancellationToken);
        }

        Assert.Equal(expected, actual);
    }

    private static string Normalise(string json)
        => json.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd() + "\n";

    private ServiceProvider BuildProvider()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:MetricsDb"] = fixture.ConnectionString,
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        services.AddLogging();

        // Production, so a masked internal error cannot leak a stack trace into a snapshot that
        // then gets committed - and so the snapshots describe what a deployed Gateway returns.
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Production);
        services.AddSingleton(environment);

        services.AddApplication(configuration);
        services.AddInfrastructure(configuration);
        services.AddPresentation(configuration);

        return services.BuildServiceProvider();
    }
}
