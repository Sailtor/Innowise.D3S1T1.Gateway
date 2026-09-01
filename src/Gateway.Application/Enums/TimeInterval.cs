namespace Gateway.Application.Enums;

/// <summary>
/// Bucket width for time aggregation. Buckets are cut on ReceivedAtUtc - the pipeline timestamp -
/// because the upstream API exposes no observation time.
/// </summary>
public enum TimeInterval
{
    /// <summary>
    /// One bucket per minute.
    /// </summary>
    Minute = 0,

    /// <summary>
    /// One bucket per hour.
    /// </summary>
    Hour = 1,

    /// <summary>
    /// One bucket per day.
    /// </summary>
    Day = 2,
}
