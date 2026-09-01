using Gateway.Application.Enums;

namespace Gateway.Application.Models;

/// <summary>
/// What to aggregate, over which slice, grouped how.
/// <para>
/// There is deliberately no "group by reading type": every <see cref="AggregationField"/> belongs
/// to exactly one type, so grouping by it would always produce a single group. The field already
/// determines the type, and the query filters on it.
/// </para>
/// </summary>
public sealed record MetricAggregationQuery
{
    /// <summary>
    /// Gets the numeric to aggregate. Also selects which reading type is scanned.
    /// </summary>
    public required AggregationField Field { get; init; }

    /// <summary>
    /// Gets the inclusive lower bound on ReceivedAtUtc.
    /// </summary>
    public DateTime? From { get; init; }

    /// <summary>
    /// Gets the exclusive upper bound on ReceivedAtUtc.
    /// </summary>
    public DateTime? To { get; init; }

    /// <summary>
    /// Gets the rooms to restrict to. Null or empty means all rooms.
    /// </summary>
    public IReadOnlyList<string>? Rooms { get; init; }

    /// <summary>
    /// Gets a value indicating whether to produce one bucket per room.
    /// </summary>
    public bool GroupByRoom { get; init; }

    /// <summary>
    /// Gets the time bucket width. Null means no time grouping.
    /// </summary>
    public TimeInterval? Interval { get; init; }
}
