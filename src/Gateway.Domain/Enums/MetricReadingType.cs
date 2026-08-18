namespace Gateway.Domain.Enums;

/// <summary>
/// Discriminator for a metric reading. Values mirror the DataProcessor write model exactly:
/// changing a member name changes the string persisted in the ReadingType column.
/// </summary>
public enum MetricReadingType
{
    /// <summary>
    /// No metric reading type specified.
    /// </summary>
    None = 0,

    /// <summary>
    /// An energy consumption reading.
    /// </summary>
    Energy = 1,

    /// <summary>
    /// An air quality reading.
    /// </summary>
    AirQuality = 2,

    /// <summary>
    /// A motion detection reading.
    /// </summary>
    Motion = 3,
}
