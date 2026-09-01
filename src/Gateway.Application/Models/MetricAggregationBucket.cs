namespace Gateway.Application.Models;

/// <summary>
/// One row of an aggregation result.
/// </summary>
/// <param name="Room">The room, or null when the query did not group by room.</param>
/// <param name="BucketStart">Inclusive start of the time bucket, or null when there was no time grouping.</param>
/// <param name="Stats">The aggregates for this bucket.</param>
public sealed record MetricAggregationBucket(
    string? Room,
    DateTime? BucketStart,
    NumericStats Stats);
