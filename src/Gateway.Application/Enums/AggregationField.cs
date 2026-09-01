namespace Gateway.Application.Enums;

/// <summary>
/// The numeric a client can aggregate over. Each member belongs to exactly one reading type,
/// which is what lets the query pre-filter on the indexed discriminator instead of casting.
/// </summary>
public enum AggregationField
{
    /// <summary>
    /// Energy consumed. Energy readings only.
    /// </summary>
    EnergyAmount = 0,

    /// <summary>
    /// Carbon dioxide concentration. Air quality readings only.
    /// </summary>
    Co2 = 1,

    /// <summary>
    /// Particulate matter under 2.5 micrometres. Air quality readings only.
    /// </summary>
    Pm25 = 2,

    /// <summary>
    /// Relative humidity. Air quality readings only.
    /// </summary>
    Humidity = 3,

    /// <summary>
    /// Motion, as 1 when detected and 0 when not. Motion readings only.
    /// Sum is therefore the number of detections and average is the detection rate.
    /// </summary>
    MotionDetected = 4,
}
