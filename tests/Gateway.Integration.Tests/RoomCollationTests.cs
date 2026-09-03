using Gateway.Application.Models;
using Gateway.Application.Validation;
using Gateway.Domain.Entities;
using Gateway.Infrastructure.Persistence;
using Gateway.Infrastructure.Queries;
using Gateway.Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Gateway.Integration.Tests;

/// <summary>
/// What happens when one room is spelled several ways.
/// <para>
/// The room summary is assembled from a SQL grouping and then regrouped in memory, and the two
/// have to agree about what counts as one room. SQL Server folds the key under the column's
/// collation - case-, accent- and trailing-space-insensitive by default - so the in-memory pass
/// uses OrdinalIgnoreCase to match. Get that wrong and the dashboard shows the same room three
/// times with its readings divided between the cards: no exception, no log, just wrong numbers
/// that look plausible.
/// </para>
/// <para>
/// These run against their own database (see <see cref="SqlServerFixture.MixedCaseConnectionString"/>)
/// because a mixed-case room in the shared seed would make SQL return an arbitrary representative
/// and turn every other room-name assertion in the suite nondeterministic.
/// </para>
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class RoomCollationTests(SqlServerFixture fixture) : IDisposable
{
    private readonly MemoryCache cache = new(new MemoryCacheOptions());

    [Fact]
    public async Task CollapsesRoomSpellingVariantsIntoOneSummary()
    {
        IReadOnlyList<RoomSummary> summaries = await CreateService()
            .GetRoomSummariesAsync(TestContext.Current.CancellationToken);

        // The tripwire. SQL has already folded the casings, so an ordinal in-memory grouping gets
        // three rows and returns three summaries - kitchens of 3 and 1, plus office - and every
        // assertion below would still pass on one of the two kitchens.
        Assert.Equal(2, summaries.Count);

        RoomSummary kitchen = Assert.Single(
            summaries,
            summary => summary.Room.Equals("kitchen", StringComparison.OrdinalIgnoreCase));

        // 2 'Kitchen' + 1 'kitchen' + 1 'KITCHEN', not the 2 or 1 of any single spelling.
        Assert.Equal(4, kitchen.TotalReadings);
    }

    [Fact]
    public async Task PicksTheNewestReadingAcrossSpellingVariants()
    {
        IReadOnlyList<RoomSummary> summaries = await CreateService()
            .GetRoomSummariesAsync(TestContext.Current.CancellationToken);

        RoomSummary kitchen = Assert.Single(
            summaries,
            summary => summary.Room.Equals("kitchen", StringComparison.OrdinalIgnoreCase));

        // The 12:00 energy row is spelled 'KITCHEN' while the air-quality rows are not. If the
        // merge dropped a variant, the latest reading would be the 11:00 air quality instead.
        EnergyReading latest = Assert.IsType<EnergyReading>(kitchen.LatestReading);
        Assert.Equal(3.0d, latest.EnergyAmount);

        Assert.Equal(2, kitchen.LatestByType.Count);
    }

    [Fact]
    public async Task ListsOneRoomNamePerSpellingVariantGroup()
    {
        IReadOnlyList<string> rooms = await CreateService()
            .GetRoomsAsync(TestContext.Current.CancellationToken);

        // SELECT DISTINCT folds the three casings server-side, so the filter dropdown gets two
        // entries rather than four. Which casing represents the kitchen is chosen by the query
        // plan and is deliberately not asserted - that is the database's business, and pinning it
        // would make this test fail on an index change that broke nothing.
        Assert.Equal(2, rooms.Count);
        Assert.Single(rooms, room => room.Equals("kitchen", StringComparison.OrdinalIgnoreCase));
        Assert.Single(rooms, room => room.Equals("office", StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public void Dispose() => cache.Dispose();

    private MetricReadingQueryService CreateService()
    {
        DbContextOptions<MetricsReadDbContext> options =
            new DbContextOptionsBuilder<MetricsReadDbContext>()
                .UseSqlServer(fixture.MixedCaseConnectionString)
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTrackingWithIdentityResolution)
                .Options;

        return new MetricReadingQueryService(
            new TestDbContextFactory(options),
            new MetricAggregationQueryValidator(),
            cache);
    }
}
