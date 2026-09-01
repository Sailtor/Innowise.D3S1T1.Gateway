using Gateway.Application.Enums;
using Gateway.Application.Interfaces;
using Gateway.Application.Models;
using Gateway.Application.Validation;
using Gateway.Domain.Entities;
using Gateway.Domain.Enums;
using Gateway.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Gateway.Infrastructure.Queries;

/// <summary>
/// Aggregation and dashboard queries, each opening and disposing its own short-lived context so
/// sibling root fields can execute in parallel without sharing one.
/// </summary>
internal sealed class MetricReadingQueryService(
    IDbContextFactory<MetricsReadDbContext> contextFactory,
    IMemoryCache cache) : IMetricReadingQueryService
{
    private const string RoomsCacheKey = "gateway:available-rooms";

    /// <summary>
    /// Fixed origin for bucket ordinals. SQL Server has no date_trunc, and constructing a DateTime
    /// inside a query does not translate, so buckets are counted as DATEDIFF units from here and
    /// converted back to a DateTime after materialisation.
    /// </summary>
    private static readonly DateTime Epoch = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly TimeSpan RoomsCacheDuration = TimeSpan.FromSeconds(30);

    public async Task<IReadOnlyList<MetricAggregationBucket>> AggregateAsync(
        MetricAggregationQuery query,
        CancellationToken cancellationToken = default)
    {
        MetricAggregationQueryValidator.Validate(query);

        await using MetricsReadDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        IQueryable<BucketedRow> rows = ApplyBucket(SelectValues(context.MetricReadings, query), query.Interval);

        List<GroupedRow> grouped = await GroupAndAggregate(rows, query).ToListAsync(cancellationToken);

        // GROUP BY yields no group for an empty input, so an ungrouped query over a window with no
        // readings would come back as no buckets at all. A client asking "average CO2 this hour"
        // wants to be told zero, not handed an empty list to interpret.
        if (grouped.Count == 0 && !query.GroupByRoom && query.Interval is null)
        {
            return [new MetricAggregationBucket(null, null, new NumericStats(0, null, null, null, null))];
        }

        return
        [
            .. grouped
                .Select(row => new MetricAggregationBucket(
                    row.Room,
                    ToBucketStart(row.Bucket, query.Interval),
                    new NumericStats(row.Count, row.Min, row.Max, row.Average, row.Sum)))
                .OrderBy(bucket => bucket.Room, StringComparer.Ordinal)
                .ThenBy(bucket => bucket.BucketStart),
        ];
    }

    public async Task<IReadOnlyList<MetricReading>> GetLatestAsync(
        IReadOnlyCollection<string>? rooms,
        IReadOnlyCollection<MetricReadingType>? types,
        CancellationToken cancellationToken = default)
    {
        await using MetricsReadDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        IQueryable<MetricReading> source = context.MetricReadings;

        if (rooms is { Count: > 0 })
        {
            List<string> wanted = [.. rooms];
            source = source.Where(reading => wanted.Contains(reading.Room));
        }

        if (types is { Count: > 0 })
        {
            List<MetricReadingType> wanted = [.. types];
            source = source.Where(reading => wanted.Contains(reading.ReadingType));
        }

        return [.. (await LatestPerRoomAndTypeAsync(source, cancellationToken))
            .OrderBy(reading => reading.Room, StringComparer.Ordinal)
            .ThenBy(reading => reading.ReadingType)];
    }

    public async Task<IReadOnlyList<RoomSummary>> GetRoomSummariesAsync(
        CancellationToken cancellationToken = default)
    {
        await using MetricsReadDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // One grouping, one key set, on purpose. Counting in one query and fetching the latest in
        // another would mean joining two independent SQL groupings on a string key in memory - and
        // SQL Server folds Room under the column's collation, which is case-, accent- and
        // trailing-space-insensitive by default, returning one arbitrary representative per plan.
        // Two plans need not choose the same one, and the mismatch would surface as a room card
        // showing a healthy total next to a blank latest reading. No exception, no log.
        List<RoomTypeSummary> perRoomAndType = await context.MetricReadings
            .GroupBy(reading => new { reading.Room, reading.ReadingType })
            .Select(group => new RoomTypeSummary
            {
                Room = group.Key.Room,
                Count = group.Count(),
                Latest = group.OrderByDescending(reading => reading.ReceivedAtUtc).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        // Ordinal is safe now: every key came out of the same query, so the representatives match.
        return
        [
            .. perRoomAndType
                .GroupBy(row => row.Room, StringComparer.Ordinal)
                .Select(BuildSummary)
                .OrderBy(summary => summary.Room, StringComparer.Ordinal),
        ];
    }

    public async Task<IReadOnlyList<string>> GetRoomsAsync(CancellationToken cancellationToken = default)
    {
        // Hit on every dashboard load and effectively immutable; 30 seconds of staleness in a
        // filter dropdown is not worth a round trip per page view.
        if (cache.TryGetValue(RoomsCacheKey, out IReadOnlyList<string>? cached) && cached is not null)
        {
            return cached;
        }

        await using MetricsReadDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        List<string> rooms = await context.MetricReadings
            .Select(reading => reading.Room)
            .Distinct()
            .OrderBy(room => room)
            .ToListAsync(cancellationToken);

        cache.Set(RoomsCacheKey, (IReadOnlyList<string>)rooms, RoomsCacheDuration);

        return rooms;
    }

    /// <summary>
    /// Narrows to the one reading type that owns the requested field and flattens it to a value.
    /// <para>
    /// OfType is doing real work here. Casting instead - (r as EnergyReading)!.EnergyAmount - adds
    /// no discriminator predicate, so rows of the other two types contribute NULL, SQL aggregates
    /// silently skip NULLs, and the result is a plausible-looking wrong average. OfType emits a
    /// WHERE on the indexed discriminator column, which is both correct and faster.
    /// </para>
    /// </summary>
    /// <param name="source">All readings.</param>
    /// <param name="query">The aggregation request.</param>
    /// <returns>One row per matching reading, carrying only the aggregated numeric.</returns>
    private static IQueryable<ValueRow> SelectValues(IQueryable<MetricReading> source, MetricAggregationQuery query)
    {
        IQueryable<MetricReading> filtered = source;

        if (query.From is { } from)
        {
            filtered = filtered.Where(reading => reading.ReceivedAtUtc >= from);
        }

        if (query.To is { } to)
        {
            filtered = filtered.Where(reading => reading.ReceivedAtUtc < to);
        }

        if (query.Rooms is { Count: > 0 })
        {
            List<string> wanted = [.. query.Rooms];
            filtered = filtered.Where(reading => wanted.Contains(reading.Room));
        }

        return query.Field switch
        {
            AggregationField.EnergyAmount => filtered.OfType<EnergyReading>()
                .Select(r => new ValueRow { Room = r.Room, ReceivedAtUtc = r.ReceivedAtUtc, Value = r.EnergyAmount }),
            AggregationField.Co2 => filtered.OfType<AirQualityReading>()
                .Select(r => new ValueRow { Room = r.Room, ReceivedAtUtc = r.ReceivedAtUtc, Value = r.Co2 }),
            AggregationField.Pm25 => filtered.OfType<AirQualityReading>()
                .Select(r => new ValueRow { Room = r.Room, ReceivedAtUtc = r.ReceivedAtUtc, Value = r.Pm25 }),
            AggregationField.Humidity => filtered.OfType<AirQualityReading>()
                .Select(r => new ValueRow { Room = r.Room, ReceivedAtUtc = r.ReceivedAtUtc, Value = r.Humidity }),
            AggregationField.MotionDetected => filtered.OfType<MotionReading>()
                .Select(r => new ValueRow
                {
                    Room = r.Room,
                    ReceivedAtUtc = r.ReceivedAtUtc,
                    Value = r.IsMotionDetected ? 1d : 0d,
                }),
            _ => throw new ArgumentOutOfRangeException(nameof(query), query.Field, "Unknown aggregation field."),
        };
    }

    /// <summary>
    /// Resolves each row's time bucket to a DATEDIFF ordinal from the epoch. When there is no time
    /// grouping the bucket is a constant, and the group key below simply ignores it.
    /// </summary>
    /// <param name="rows">Flattened value rows.</param>
    /// <param name="interval">Bucket width, or null for no time grouping.</param>
    /// <returns>The same rows with a bucket ordinal attached.</returns>
    private static IQueryable<BucketedRow> ApplyBucket(IQueryable<ValueRow> rows, TimeInterval? interval)
        => interval switch
        {
            TimeInterval.Minute => rows.Select(r => new BucketedRow
            {
                Room = r.Room,
                Bucket = EF.Functions.DateDiffMinute(Epoch, r.ReceivedAtUtc),
                Value = r.Value,
            }),
            TimeInterval.Hour => rows.Select(r => new BucketedRow
            {
                Room = r.Room,
                Bucket = EF.Functions.DateDiffHour(Epoch, r.ReceivedAtUtc),
                Value = r.Value,
            }),
            TimeInterval.Day => rows.Select(r => new BucketedRow
            {
                Room = r.Room,
                Bucket = EF.Functions.DateDiffDay(Epoch, r.ReceivedAtUtc),
                Value = r.Value,
            }),
            _ => rows.Select(r => new BucketedRow { Room = r.Room, Bucket = 0, Value = r.Value }),
        };

    /// <summary>
    /// The four grouping shapes, written out rather than composed.
    /// <para>
    /// Anonymous group keys, deliberately: EF is best-tested with them, and every aggregate lambda
    /// has to be an inline literal anyway. Each branch ends in an aggregating Select - a chain
    /// ending at GroupBy would be a *final* GroupBy, which EF has evaluated on the client since
    /// version 7, quietly pulling every row into memory.
    /// </para>
    /// </summary>
    /// <param name="rows">Bucketed value rows.</param>
    /// <param name="query">The aggregation request, which decides the grouping shape.</param>
    /// <returns>One row per group.</returns>
    private static IQueryable<GroupedRow> GroupAndAggregate(IQueryable<BucketedRow> rows, MetricAggregationQuery query)
        => (query.GroupByRoom, query.Interval is not null) switch
        {
            (true, true) => rows
                .GroupBy(row => new { row.Room, row.Bucket })
                .Select(g => new GroupedRow
                {
                    Room = g.Key.Room,
                    Bucket = g.Key.Bucket,
                    Count = g.Count(),
                    Min = g.Min(x => (double?)x.Value),
                    Max = g.Max(x => (double?)x.Value),
                    Average = g.Average(x => (double?)x.Value),
                    Sum = g.Sum(x => (double?)x.Value),
                }),
            (true, false) => rows
                .GroupBy(row => row.Room)
                .Select(g => new GroupedRow
                {
                    Room = g.Key,
                    Bucket = null,
                    Count = g.Count(),
                    Min = g.Min(x => (double?)x.Value),
                    Max = g.Max(x => (double?)x.Value),
                    Average = g.Average(x => (double?)x.Value),
                    Sum = g.Sum(x => (double?)x.Value),
                }),
            (false, true) => rows
                .GroupBy(row => row.Bucket)
                .Select(g => new GroupedRow
                {
                    Room = null,
                    Bucket = g.Key,
                    Count = g.Count(),
                    Min = g.Min(x => (double?)x.Value),
                    Max = g.Max(x => (double?)x.Value),
                    Average = g.Average(x => (double?)x.Value),
                    Sum = g.Sum(x => (double?)x.Value),
                }),
            _ => rows
                .GroupBy(_ => 1)
                .Select(g => new GroupedRow
                {
                    Room = null,
                    Bucket = null,
                    Count = g.Count(),
                    Min = g.Min(x => (double?)x.Value),
                    Max = g.Max(x => (double?)x.Value),
                    Average = g.Average(x => (double?)x.Value),
                    Sum = g.Sum(x => (double?)x.Value),
                }),
        };

    /// <summary>
    /// Latest reading per room and type.
    /// <para>
    /// GroupBy(key).Select(g => g.OrderByDescending(...).FirstOrDefault()) does translate on EF 10,
    /// to a ROW_NUMBER window over a GROUP BY derived table. Composing anything on top of it -
    /// another Select, a Where, a Join - commonly does not, and the fix is milestoned for EF 11.
    /// So this materialises immediately and everything else happens on the client.
    /// </para>
    /// </summary>
    /// <param name="source">Readings to consider, already filtered.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>At most one reading per room and type.</returns>
    private static async Task<IReadOnlyList<MetricReading>> LatestPerRoomAndTypeAsync(
        IQueryable<MetricReading> source,
        CancellationToken cancellationToken)
    {
        List<MetricReading?> latest = await source
            .GroupBy(reading => new { reading.Room, reading.ReadingType })
            .Select(group => group.OrderByDescending(reading => reading.ReceivedAtUtc).FirstOrDefault())
            .ToListAsync(cancellationToken);

        return [.. latest.OfType<MetricReading>()];
    }

    private static RoomSummary BuildSummary(IGrouping<string, RoomTypeSummary> byRoom)
    {
        List<MetricReading> latest =
        [
            .. byRoom
                .Select(row => row.Latest)
                .OfType<MetricReading>()
                .OrderBy(reading => reading.ReadingType),
        ];

        return new RoomSummary(
            byRoom.Key,
            byRoom.Sum(row => row.Count),
            latest.MaxBy(reading => reading.ReceivedAtUtc),
            latest);
    }

    private static DateTime? ToBucketStart(int? bucket, TimeInterval? interval)
        => (bucket, interval) switch
        {
            (null, _) => null,
            (_, null) => null,
            ({ } ordinal, TimeInterval.Minute) => Epoch.AddMinutes(ordinal),
            ({ } ordinal, TimeInterval.Hour) => Epoch.AddHours(ordinal),
            ({ } ordinal, TimeInterval.Day) => Epoch.AddDays(ordinal),
            _ => null,
        };
}
