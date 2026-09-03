using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Gateway.Integration.Tests.Infrastructure;

/// <summary>
/// A real SQL Server carrying DataProcessor's schema and a known set of readings.
/// <para>
/// This is the contract between the two services made executable. The Gateway declares its own
/// read model rather than sharing a package with the writer, which buys independence at the cost
/// of a mapping that can drift silently. Everything in this collection exists to make that drift
/// fail a build instead of a dashboard.
/// </para>
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    /// <summary>
    /// Pinned rather than floating: this is the exact tag Testcontainers 4.14 pins internally, so
    /// it is guaranteed to exist and to match the wait strategy it was tested against.
    /// </summary>
    private const string SqlServerImage = "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04";

    private const string DatabaseName = "GatewayIntegrationTests";

    /// <summary>
    /// Two rooms, all three reading types, values chosen so every expected aggregate can be worked
    /// out by hand. The cross-type rows are the point: an aggregate over Co2 must ignore the energy
    /// and motion rows entirely, and only real data proves it.
    /// </summary>
    private const string SeedSql = """
        INSERT INTO [MetricReadings]
            ([Room], [IngestedAtUtc], [ReceivedAtUtc], [ReadingType], [Co2], [Pm25], [Humidity], [EnergyAmount], [IsMotionDetected])
        VALUES
            ('kitchen', '2026-08-18T10:00:00', '2026-08-18T10:00:00', 'AirQuality', 400, 5, 40, NULL, NULL),
            ('kitchen', '2026-08-18T10:30:00', '2026-08-18T10:30:00', 'AirQuality', 500, 7, 45, NULL, NULL),
            ('kitchen', '2026-08-18T11:30:00', '2026-08-18T11:30:00', 'AirQuality', 600, 9, 50, NULL, NULL),
            ('kitchen', '2026-08-18T10:00:00', '2026-08-18T10:00:00', 'Energy',     NULL, NULL, NULL, 1.5, NULL),
            ('kitchen', '2026-08-18T11:45:00', '2026-08-18T11:45:00', 'Energy',     NULL, NULL, NULL, 2.5, NULL),
            ('office',  '2026-08-18T10:00:00', '2026-08-18T10:00:00', 'AirQuality', 1000, 1, 30, NULL, NULL),
            ('office',  '2026-08-18T10:00:00', '2026-08-18T10:00:00', 'Motion',     NULL, NULL, NULL, NULL, 1),
            ('office',  '2026-08-18T11:30:00', '2026-08-18T11:30:00', 'Motion',     NULL, NULL, NULL, NULL, 0);
        """;

    private readonly MsSqlContainer container = new MsSqlBuilder(SqlServerImage).Build();

    /// <summary>
    /// Gets the connection string for the seeded database.
    /// </summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        // Generous and independent of the per-test token: a cold image pull can take minutes, and
        // a test-scoped cancellation would kill it half way through.
        using CancellationTokenSource cancellation = new(TimeSpan.FromMinutes(5));

        await container.StartAsync(cancellation.Token);

        // The image starts with no user database, so GetConnectionString() points at master.
        await ExecuteAsync(container.GetConnectionString(), $"CREATE DATABASE [{DatabaseName}];", cancellation.Token);

        ConnectionString = new SqlConnectionStringBuilder(container.GetConnectionString())
        {
            InitialCatalog = DatabaseName,
        }.ConnectionString;

        await ExecuteAsync(ConnectionString, await ReadSchemaAsync(cancellation.Token), cancellation.Token);
        await ExecuteAsync(ConnectionString, SeedSql, cancellation.Token);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => container.DisposeAsync();

    private static async Task<string> ReadSchemaAsync(CancellationToken cancellationToken)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Schema", "DataProcessorSchema.sql");

        return await File.ReadAllTextAsync(path, cancellationToken);
    }

    private static async Task ExecuteAsync(string connectionString, string sql, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = sql;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
