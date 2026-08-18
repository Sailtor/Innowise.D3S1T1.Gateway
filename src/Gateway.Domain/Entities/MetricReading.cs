using Gateway.Domain.Enums;

namespace Gateway.Domain.Entities;

/// <summary>
/// A single reading produced by the ingestion pipeline. Materialised by EF Core from the
/// MetricReadings table; never constructed by the Gateway itself, hence init-only members.
/// </summary>
public abstract class MetricReading
{
    public long Id { get; init; }

    public string Room { get; init; } = string.Empty;

    /// <summary>
    /// Gets the mapped TPH discriminator. Exposing it as a real property (rather than the shadow
    /// property the writer uses) is what lets clients filter and sort by reading type without a
    /// CLR cast, and lets aggregations pre-filter on an indexable column.
    /// Surfaced to GraphQL as `type`; named for its column here to avoid colliding with GetType().
    /// </summary>
    public MetricReadingType ReadingType { get; init; }

    /// <summary>
    /// Gets the moment the ingestor pulled the batch from the upstream API.
    /// Pipeline time, not observation time: the upstream API exposes no sensor timestamp.
    /// </summary>
    public DateTime IngestedAtUtc { get; init; }

    /// <summary>
    /// Gets the moment the processor consumed the message. This is the column time aggregations bucket on.
    /// </summary>
    public DateTime ReceivedAtUtc { get; init; }
}
