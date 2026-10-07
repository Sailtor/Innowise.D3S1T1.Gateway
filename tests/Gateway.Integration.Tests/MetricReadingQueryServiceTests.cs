using FluentValidation;
using Gateway.Application.Enums;
using Gateway.Application.Models;
using Gateway.Application.Validation;
using Gateway.Domain.Entities;
using Gateway.Domain.Enums;
using Gateway.Infrastructure.Persistence;
using Gateway.Infrastructure.Queries;
using Gateway.Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Gateway.Integration.Tests;

/// <summary>
/// Every dashboard query, against a real SQL Server carrying DataProcessor's schema.
/// <para>
/// This is the drift guard §2 of the plan calls the contract between the two services. The Gateway
/// declares its own read model instead of sharing a package with the writer, so a renamed column
/// still compiles and only fails at runtime. These tests are what turn that into a build failure.
/// </para>
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class MetricReadingQueryServiceTests(SqlServerFixture fixture) : IDisposable
{
    private static readonly DateTime WindowStart = new(2026, 8, 18, 0, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime WindowEnd = new(2026, 8, 19, 0, 0, 0, DateTimeKind.Utc);

    private readonly SqlCapturingInterceptor sql = new();

    // One per test instance - xunit constructs the class per test, so each still gets a cold
    // cache, and it is disposed with the instance rather than leaking an expiration timer.
    private readonly MemoryCache cache = new(new MemoryCacheOptions());

    [Fact]
    public async Task MaterialisesEveryConcreteTypeFromTheWritersSchema()
    {
        // The strongest single drift assertion: it only passes if the discriminator column, its
        // string values and every payload column name still match what DataProcessor writes.
        IReadOnlyList<MetricReading> latest = await CreateService()
            .GetLatestAsync(null, null, TestContext.Current.CancellationToken);

        Assert.Contains(latest, reading => reading is AirQualityReading { Co2: 600d });
        Assert.Contains(latest, reading => reading is EnergyReading { EnergyAmount: 2.5d });
        Assert.Contains(latest, reading => reading is MotionReading);
    }

    [Fact]
    public async Task MaterialisesTimestampsAsUtc()
    {
        // datetime2 carries no offset, so without the value converter added in Phase 2 these come
        // back Unspecified and the GraphQL scalar stamps them with the server's local offset.
        IReadOnlyList<MetricReading> latest = await CreateService()
            .GetLatestAsync(null, null, TestContext.Current.CancellationToken);

        Assert.NotEmpty(latest);
        Assert.All(latest, reading => Assert.Equal(DateTimeKind.Utc, reading.ReceivedAtUtc.Kind));
        Assert.All(latest, reading => Assert.Equal(DateTimeKind.Utc, reading.IngestedAtUtc.Kind));
    }

    [Fact]
    public async Task AggregatesOnlyTheReadingTypeThatOwnsTheField()
    {
        // The seed holds two energy rows and two motion rows alongside four air-quality rows. A
        // cast instead of OfType would let those contribute NULLs, SQL would skip them, and the
        // count would silently be 8 with a different average. This is that bug's tripwire.
        IReadOnlyList<MetricAggregationBucket> buckets = await CreateService().AggregateAsync(
            new MetricAggregationQuery { Field = AggregationField.Co2 },
            TestContext.Current.CancellationToken);

        MetricAggregationBucket bucket = Assert.Single(buckets);

        Assert.Equal(4, bucket.Stats.Count);
        Assert.Equal(400d, bucket.Stats.Min);
        Assert.Equal(1000d, bucket.Stats.Max);
        Assert.Equal(2500d, bucket.Stats.Sum);
        Assert.Equal(625d, bucket.Stats.Average);

        // Narrowing must happen in SQL on the indexed discriminator, not as a CASE over the table.
        Assert.Contains(sql.Commands, command => command.Contains("ReadingType", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GroupsByRoom()
    {
        IReadOnlyList<MetricAggregationBucket> buckets = await CreateService().AggregateAsync(
            new MetricAggregationQuery { Field = AggregationField.Co2, GroupByRoom = true },
            TestContext.Current.CancellationToken);

        Assert.Equal(2, buckets.Count);

        MetricAggregationBucket kitchen = Assert.Single(buckets, b => b.Room == "kitchen");
        Assert.Equal(3, kitchen.Stats.Count);
        Assert.Equal(500d, kitchen.Stats.Average);

        MetricAggregationBucket office = Assert.Single(buckets, b => b.Room == "office");
        Assert.Equal(1, office.Stats.Count);
        Assert.Equal(1000d, office.Stats.Average);
    }

    [Fact]
    public async Task BucketsByHourWithDateDiffAndReturnsRealBucketStarts()
    {
        IReadOnlyList<MetricAggregationBucket> buckets = await CreateService().AggregateAsync(
            new MetricAggregationQuery
            {
                Field = AggregationField.Co2,
                From = WindowStart,
                To = WindowEnd,
                Interval = TimeInterval.Hour,
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(2, buckets.Count);

        MetricAggregationBucket tenth = Assert.Single(
            buckets, b => b.BucketStart == new DateTime(2026, 8, 18, 10, 0, 0, DateTimeKind.Utc));
        Assert.Equal(3, tenth.Stats.Count);

        MetricAggregationBucket eleventh = Assert.Single(
            buckets, b => b.BucketStart == new DateTime(2026, 8, 18, 11, 0, 0, DateTimeKind.Utc));
        Assert.Equal(1, eleventh.Stats.Count);
        Assert.Equal(600d, eleventh.Stats.Average);

        // Bucketing has to be a server-side DATEDIFF. Client evaluation would return the same
        // numbers on eight rows and pull the whole table on a real one.
        Assert.Contains(sql.Commands, command => command.Contains("DATEDIFF", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TreatsMotionAsOneAndZeroSoSumIsDetectionsAndAverageIsTheRate()
    {
        IReadOnlyList<MetricAggregationBucket> buckets = await CreateService().AggregateAsync(
            new MetricAggregationQuery { Field = AggregationField.MotionDetected },
            TestContext.Current.CancellationToken);

        MetricAggregationBucket bucket = Assert.Single(buckets);

        Assert.Equal(2, bucket.Stats.Count);
        Assert.Equal(1d, bucket.Stats.Sum);
        Assert.Equal(0.5d, bucket.Stats.Average);
    }

    [Fact]
    public async Task ReportsAnEmptyWindowAsZeroRatherThanNoBuckets()
    {
        IReadOnlyList<MetricAggregationBucket> buckets = await CreateService().AggregateAsync(
            new MetricAggregationQuery
            {
                Field = AggregationField.Co2,
                From = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                To = new DateTime(2020, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            },
            TestContext.Current.CancellationToken);

        MetricAggregationBucket bucket = Assert.Single(buckets);

        Assert.Equal(0, bucket.Stats.Count);
        Assert.Null(bucket.Stats.Average);
    }

    [Fact]
    public async Task RejectsAQueryThatWouldExplodeTheBucketCount()
    {
        await Assert.ThrowsAsync<ValidationException>(() => CreateService().AggregateAsync(
            new MetricAggregationQuery
            {
                Field = AggregationField.Co2,
                Interval = TimeInterval.Minute,
            },
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReturnsTheLatestReadingPerRoomAndTypeUsingAWindowFunction()
    {
        IReadOnlyList<MetricReading> latest = await CreateService()
            .GetLatestAsync(null, null, TestContext.Current.CancellationToken);

        Assert.Equal(4, latest.Count);

        MetricReading kitchenAir = Assert.Single(
            latest, r => r.Room == "kitchen" && r.ReadingType == MetricReadingType.AirQuality);
        Assert.Equal(new DateTime(2026, 8, 18, 11, 30, 0, DateTimeKind.Utc), kitchenAir.ReceivedAtUtc);

        MetricReading officeMotion = Assert.Single(
            latest, r => r.Room == "office" && r.ReadingType == MetricReadingType.Motion);
        Assert.Equal(new DateTime(2026, 8, 18, 11, 30, 0, DateTimeKind.Utc), officeMotion.ReceivedAtUtc);

        Assert.Contains(sql.Commands, command => command.Contains("ROW_NUMBER", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task FiltersTheLatestReadingsByRoomAndType()
    {
        IReadOnlyList<MetricReading> latest = await CreateService().GetLatestAsync(
            ["kitchen"],
            [MetricReadingType.Energy],
            TestContext.Current.CancellationToken);

        EnergyReading reading = Assert.IsType<EnergyReading>(Assert.Single(latest));

        Assert.Equal(2.5d, reading.EnergyAmount);
    }

    [Fact]
    public async Task SummarisesEachRoomWithItsTotalAndLatestReadings()
    {
        IReadOnlyList<RoomSummary> summaries = await CreateService()
            .GetRoomSummariesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, summaries.Count);

        RoomSummary kitchen = Assert.Single(summaries, s => s.Room == "kitchen");
        Assert.Equal(5, kitchen.TotalReadings);
        Assert.Equal(2, kitchen.LatestByType.Count);

        // The newest of any type in that room - energy at 11:45, later than the 11:30 air quality.
        Assert.IsType<EnergyReading>(kitchen.LatestReading);

        RoomSummary office = Assert.Single(summaries, s => s.Room == "office");
        Assert.Equal(3, office.TotalReadings);
        Assert.Equal(2, office.LatestByType.Count);
        Assert.IsType<MotionReading>(office.LatestReading);
    }

    [Fact]
    public async Task ReturnsDistinctRoomsInOrder()
    {
        IReadOnlyList<string> rooms = await CreateService()
            .GetRoomsAsync(TestContext.Current.CancellationToken);

        Assert.Equal<string>(["kitchen", "office"], rooms);
    }

    [Fact]
    public async Task ReturnsAllEightSeededReadingsByDefault()
    {
        ReadingsPage page = await CreateService()
            .GetReadingsAsync(new ReadingsQuery { Take = 20 }, TestContext.Current.CancellationToken);

        Assert.Equal(8, page.TotalCount);
        Assert.Equal(8, page.Items.Count);
    }

    [Fact]
    public async Task FiltersReadingsByRoom()
    {
        ReadingsPage page = await CreateService().GetReadingsAsync(
            new ReadingsQuery { Room = "office" }, TestContext.Current.CancellationToken);

        Assert.Equal(3, page.TotalCount);
        Assert.All(page.Items, reading => Assert.Equal("office", reading.Room));
    }

    [Fact]
    public async Task FiltersReadingsByType()
    {
        ReadingsPage page = await CreateService().GetReadingsAsync(
            new ReadingsQuery { Type = MetricReadingType.Energy }, TestContext.Current.CancellationToken);

        Assert.Equal(2, page.TotalCount);
        Assert.All(page.Items, reading => Assert.IsType<EnergyReading>(reading));
    }

    [Fact]
    public async Task FiltersReadingsByReceivedAtRange()
    {
        // Inclusive lower / exclusive upper, same convention as the aggregation query's from/to.
        ReadingsPage page = await CreateService().GetReadingsAsync(
            new ReadingsQuery
            {
                ReceivedAtFrom = new DateTime(2026, 8, 18, 11, 0, 0, DateTimeKind.Utc),
                ReceivedAtTo = new DateTime(2026, 8, 18, 12, 0, 0, DateTimeKind.Utc),
            },
            TestContext.Current.CancellationToken);

        // kitchen AirQuality 11:30, kitchen Energy 11:45, office Motion 11:30.
        Assert.Equal(3, page.TotalCount);
    }

    [Fact]
    public async Task FiltersReadingsByIngestedAtRange()
    {
        // The seed sets IngestedAtUtc equal to ReceivedAtUtc on every row, so this exercises the
        // IngestedAt column specifically rather than re-proving the ReceivedAt case above.
        ReadingsPage page = await CreateService().GetReadingsAsync(
            new ReadingsQuery
            {
                IngestedAtFrom = new DateTime(2026, 8, 18, 11, 0, 0, DateTimeKind.Utc),
                IngestedAtTo = new DateTime(2026, 8, 18, 12, 0, 0, DateTimeKind.Utc),
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(3, page.TotalCount);
    }

    [Fact]
    public async Task CombinesRoomAndTypeFilters()
    {
        ReadingsPage page = await CreateService().GetReadingsAsync(
            new ReadingsQuery { Room = "kitchen", Type = MetricReadingType.AirQuality },
            TestContext.Current.CancellationToken);

        // Kitchen has 5 readings total and 2 Energy readings - only the 3 AirQuality ones should match.
        Assert.Equal(3, page.TotalCount);
        Assert.All(page.Items, reading =>
        {
            Assert.Equal("kitchen", reading.Room);
            Assert.IsType<AirQualityReading>(reading);
        });
    }

    [Fact]
    public async Task SortsByReceivedAtAscending()
    {
        ReadingsPage page = await CreateService().GetReadingsAsync(
            new ReadingsQuery { SortBy = ReadingSortField.ReceivedAt, SortDir = SortDirection.Ascending, Take = 20 },
            TestContext.Current.CancellationToken);

        Assert.Equal(
            new DateTime(2026, 8, 18, 10, 0, 0, DateTimeKind.Utc),
            page.Items[0].ReceivedAtUtc);
        Assert.True(page.Items.Zip(page.Items.Skip(1))
            .All(pair => pair.First.ReceivedAtUtc <= pair.Second.ReceivedAtUtc));
    }

    [Fact]
    public async Task SortsByIngestedAtAscending()
    {
        ReadingsPage page = await CreateService().GetReadingsAsync(
            new ReadingsQuery { SortBy = ReadingSortField.IngestedAt, SortDir = SortDirection.Ascending, Take = 20 },
            TestContext.Current.CancellationToken);

        Assert.Equal(
            new DateTime(2026, 8, 18, 10, 0, 0, DateTimeKind.Utc),
            page.Items[0].IngestedAtUtc);
    }

    [Fact]
    public async Task SortsByIngestedAtDescending()
    {
        ReadingsPage page = await CreateService().GetReadingsAsync(
            new ReadingsQuery { SortBy = ReadingSortField.IngestedAt, SortDir = SortDirection.Descending, Take = 20 },
            TestContext.Current.CancellationToken);

        Assert.Equal(
            new DateTime(2026, 8, 18, 11, 45, 0, DateTimeKind.Utc),
            page.Items[0].IngestedAtUtc);
    }

    [Fact]
    public async Task SortsByRoomDescendingWithOfficeBeforeKitchen()
    {
        ReadingsPage page = await CreateService().GetReadingsAsync(
            new ReadingsQuery { SortBy = ReadingSortField.Room, SortDir = SortDirection.Descending, Take = 20 },
            TestContext.Current.CancellationToken);

        Assert.Equal(3, page.Items.Take(3).Count(reading => reading.Room == "office"));
        Assert.Equal(5, page.Items.Skip(3).Count(reading => reading.Room == "kitchen"));
    }

    [Fact]
    public async Task SortsByTypeAscendingWithAirQualityFirst()
    {
        // ReadingType is stored and ordered as its nvarchar column ("AirQuality"/"Energy"/"Motion"
        // via HasConversion<string>()), not the enum's numeric value - ascending is alphabetical.
        ReadingsPage page = await CreateService().GetReadingsAsync(
            new ReadingsQuery { SortBy = ReadingSortField.Type, SortDir = SortDirection.Ascending, Take = 20 },
            TestContext.Current.CancellationToken);

        Assert.Equal(4, page.Items.Take(4).Count(reading => reading is AirQualityReading));
    }

    [Fact]
    public async Task SortsByTypeDescendingWithMotionFirst()
    {
        // Alphabetically last of the three discriminator strings, so first in descending order.
        ReadingsPage page = await CreateService().GetReadingsAsync(
            new ReadingsQuery { SortBy = ReadingSortField.Type, SortDir = SortDirection.Descending, Take = 20 },
            TestContext.Current.CancellationToken);

        Assert.Equal(2, page.Items.Take(2).Count(reading => reading is MotionReading));
    }

    [Fact]
    public async Task SortsByReceivedAtDescendingByDefault()
    {
        ReadingsPage page = await CreateService()
            .GetReadingsAsync(new ReadingsQuery { Take = 20 }, TestContext.Current.CancellationToken);

        Assert.Equal(
            new DateTime(2026, 8, 18, 11, 45, 0, DateTimeKind.Utc),
            page.Items[0].ReceivedAtUtc);
        Assert.True(page.Items.Zip(page.Items.Skip(1))
            .All(pair => pair.First.ReceivedAtUtc >= pair.Second.ReceivedAtUtc));
    }

    [Fact]
    public async Task SortsByRoomAscendingWithKitchenBeforeOffice()
    {
        ReadingsPage page = await CreateService().GetReadingsAsync(
            new ReadingsQuery { SortBy = ReadingSortField.Room, SortDir = SortDirection.Ascending, Take = 20 },
            TestContext.Current.CancellationToken);

        Assert.Equal(5, page.Items.Take(5).Count(reading => reading.Room == "kitchen"));
        Assert.Equal(3, page.Items.Skip(5).Count(reading => reading.Room == "office"));
    }

    [Fact]
    public async Task PagesWithoutDisturbingTheTotalCount()
    {
        ReadingsPage firstPage = await CreateService().GetReadingsAsync(
            new ReadingsQuery { Skip = 0, Take = 3 }, TestContext.Current.CancellationToken);
        ReadingsPage lastPage = await CreateService().GetReadingsAsync(
            new ReadingsQuery { Skip = 6, Take = 3 }, TestContext.Current.CancellationToken);

        Assert.Equal(8, firstPage.TotalCount);
        Assert.Equal(3, firstPage.Items.Count);
        Assert.Equal(8, lastPage.TotalCount);
        Assert.Equal(2, lastPage.Items.Count);
    }

    [Fact]
    public async Task RejectsATakeOutsideTheAllowedRange()
    {
        await Assert.ThrowsAsync<ValidationException>(() => CreateService().GetReadingsAsync(
            new ReadingsQuery { Take = 0 }, TestContext.Current.CancellationToken));
    }

    /// <inheritdoc />
    public void Dispose() => cache.Dispose();

    private MetricReadingQueryService CreateService()
    {
        DbContextOptions<MetricsReadDbContext> options =
            new DbContextOptionsBuilder<MetricsReadDbContext>()
                .UseSqlServer(fixture.ConnectionString)
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTrackingWithIdentityResolution)
                .AddInterceptors(sql)
                .Options;

        return new MetricReadingQueryService(
            new TestDbContextFactory(options),
            new MetricAggregationQueryValidator(),
            new ReadingsQueryValidator(),
            cache);
    }
}
