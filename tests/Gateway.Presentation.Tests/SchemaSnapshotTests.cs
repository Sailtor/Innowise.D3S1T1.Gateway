using Gateway.Application;
using Gateway.Infrastructure;
using HotChocolate;
using HotChocolate.Execution;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Gateway.Presentation.Tests;

/// <summary>
/// Pins the whole public contract as SDL.
/// <para>
/// The structural assertions elsewhere check the fields anyone thought to name. This checks
/// everything: a renamed field, a widened nullability, an accidentally-exposed property or a
/// filter operation that appears because a package updated all show up as a diff a reviewer reads
/// in the pull request, next to the change that caused it.
/// </para>
/// </summary>
public class SchemaSnapshotTests
{
    private const string SnapshotFileName = "schema.graphql";

    private const string FakeConnectionString =
        "Server=(local);Database=GatewaySchemaSnapshot;Trusted_Connection=True;TrustServerCertificate=True";

    [Fact]
    public async Task SchemaMatchesTheCommittedSnapshot()
    {
        await using ServiceProvider provider = BuildProvider();

        ISchemaDefinition schema = await provider.GetSchemaAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        string actual = Normalise(schema.ToString());

        // The output directory, not the source tree. Locating it by [CallerFilePath] passes locally
        // and fails in CI: ContinuousIntegrationBuild implies deterministic source paths, which
        // rewrite that value to a path that does not exist on disk.
        string expectedPath = System.IO.Path.Combine(AppContext.BaseDirectory, SnapshotFileName);
        string actualPath = expectedPath + ".actual";

        if (!File.Exists(expectedPath))
        {
            await File.WriteAllTextAsync(actualPath, actual, TestContext.Current.CancellationToken);

            // Deliberately a failure rather than a silent pass. A snapshot that becomes the
            // baseline without anyone reading it is not a test, and the first version of this
            // schema is exactly the one worth reviewing by hand.
            Assert.Fail(
                $"No schema snapshot exists. The current SDL was written to {actualPath}. "
                + $"Review it, copy it to {SnapshotFileName} beside the test source, commit it, and re-run.");
        }

        string expected = Normalise(
            await File.ReadAllTextAsync(expectedPath, TestContext.Current.CancellationToken));

        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            // Leave the new SDL on disk so the diff can be read rather than reconstructed.
            await File.WriteAllTextAsync(actualPath, actual, TestContext.Current.CancellationToken);
        }

        Assert.Equal(expected, actual);
    }

    private static string Normalise(string sdl)
        => sdl.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd() + "\n";

    private static ServiceProvider BuildProvider()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:MetricsDb"] = FakeConnectionString,
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

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
