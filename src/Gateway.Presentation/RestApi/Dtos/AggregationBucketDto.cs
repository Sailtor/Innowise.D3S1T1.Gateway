using Gateway.Application.Models;

namespace Gateway.Presentation.RestApi.Dtos;

/// <summary>
/// The REST wire shape for one aggregation bucket.
/// </summary>
/// <param name="Room">The room, or null when the query did not group by room.</param>
/// <param name="BucketStart">Inclusive start of the time bucket, or null when there was no time grouping.</param>
/// <param name="Stats">The aggregates for this bucket.</param>
public sealed record AggregationBucketDto(
    string? Room,
    DateTime? BucketStart,
    NumericStatsDto Stats)
{
    /// <summary>
    /// Maps the Application-layer bucket onto its REST shape.
    /// </summary>
    /// <param name="bucket">The bucket to map.</param>
    /// <returns>The equivalent <see cref="AggregationBucketDto"/>.</returns>
    public static AggregationBucketDto FromDomain(MetricAggregationBucket bucket)
        => new(bucket.Room, bucket.BucketStart, NumericStatsDto.FromDomain(bucket.Stats));
}
