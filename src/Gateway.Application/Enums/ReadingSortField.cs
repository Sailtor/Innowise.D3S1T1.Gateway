namespace Gateway.Application.Enums;

/// <summary>
/// The column a paged-readings query can sort by. Restricted to the same four dimensions
/// <c>MetricReadingFilterInputType</c>/<c>MetricReadingSortInputType</c> allowlist for GraphQL -
/// every one of them is either indexed or the discriminator column.
/// </summary>
public enum ReadingSortField
{
    /// <summary>
    /// The moment the processor consumed the message.
    /// </summary>
    ReceivedAt = 0,

    /// <summary>
    /// The moment the ingestor pulled the batch from the upstream API.
    /// </summary>
    IngestedAt = 1,

    /// <summary>
    /// The room the reading was produced in.
    /// </summary>
    Room = 2,

    /// <summary>
    /// The reading's discriminator (Energy, AirQuality, Motion).
    /// </summary>
    Type = 3,
}
