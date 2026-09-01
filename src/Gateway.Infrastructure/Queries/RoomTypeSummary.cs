using Gateway.Domain.Entities;

namespace Gateway.Infrastructure.Queries;

/// <summary>
/// Count and latest reading for one room-and-type pair, from a single grouped query.
/// Both facts come from the same grouping deliberately - see GetRoomSummariesAsync.
/// </summary>
internal sealed class RoomTypeSummary
{
    public string Room { get; init; } = string.Empty;

    public int Count { get; init; }

    public MetricReading? Latest { get; init; }
}
