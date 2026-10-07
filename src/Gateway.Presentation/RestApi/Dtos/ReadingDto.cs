using Gateway.Domain.Entities;
using Gateway.Domain.Enums;

namespace Gateway.Presentation.RestApi.Dtos;

/// <summary>
/// The REST wire shape for a reading - flat, with one nullable field per type-specific column,
/// mirroring the MetricReadings table's own TPH layout rather than a JSON-polymorphic hierarchy.
/// Putting <c>[JsonPolymorphic]</c>/<c>[JsonDerivedType]</c> on <see cref="MetricReading"/> itself
/// would add a serialization concern to a Domain project that currently has none.
/// </summary>
/// <param name="Id">The reading's identity.</param>
/// <param name="Room">The room the reading was produced in.</param>
/// <param name="Type">The reading's discriminator.</param>
/// <param name="IngestedAt">When the ingestor pulled the batch from the upstream API, in UTC.</param>
/// <param name="ReceivedAt">When the processor consumed the message, in UTC.</param>
/// <param name="EnergyAmount">Energy consumed. Populated for Energy readings only.</param>
/// <param name="Co2">Carbon dioxide concentration. Populated for AirQuality readings only.</param>
/// <param name="Pm25">Particulate matter under 2.5 micrometres. Populated for AirQuality readings only.</param>
/// <param name="Humidity">Relative humidity. Populated for AirQuality readings only.</param>
/// <param name="IsMotionDetected">Whether motion was detected. Populated for Motion readings only.</param>
public sealed record ReadingDto(
    long Id,
    string Room,
    MetricReadingType Type,
    DateTime IngestedAt,
    DateTime ReceivedAt,
    double? EnergyAmount,
    double? Co2,
    double? Pm25,
    double? Humidity,
    bool? IsMotionDetected)
{
    /// <summary>
    /// Maps a domain reading of any concrete type onto its flat REST shape.
    /// </summary>
    /// <param name="reading">The reading to map.</param>
    /// <returns>The equivalent <see cref="ReadingDto"/>.</returns>
    public static ReadingDto FromDomain(MetricReading reading) => reading switch
    {
        EnergyReading energy => new ReadingDto(
            energy.Id,
            energy.Room,
            energy.ReadingType,
            energy.IngestedAtUtc,
            energy.ReceivedAtUtc,
            EnergyAmount: energy.EnergyAmount,
            Co2: null,
            Pm25: null,
            Humidity: null,
            IsMotionDetected: null),
        AirQualityReading airQuality => new ReadingDto(
            airQuality.Id,
            airQuality.Room,
            airQuality.ReadingType,
            airQuality.IngestedAtUtc,
            airQuality.ReceivedAtUtc,
            EnergyAmount: null,
            Co2: airQuality.Co2,
            Pm25: airQuality.Pm25,
            Humidity: airQuality.Humidity,
            IsMotionDetected: null),
        MotionReading motion => new ReadingDto(
            motion.Id,
            motion.Room,
            motion.ReadingType,
            motion.IngestedAtUtc,
            motion.ReceivedAtUtc,
            EnergyAmount: null,
            Co2: null,
            Pm25: null,
            Humidity: null,
            IsMotionDetected: motion.IsMotionDetected),
        _ => throw new ArgumentOutOfRangeException(nameof(reading), reading.GetType(), "Unknown reading type."),
    };
}
