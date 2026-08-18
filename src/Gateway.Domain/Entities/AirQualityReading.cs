namespace Gateway.Domain.Entities;

public sealed class AirQualityReading : MetricReading
{
    public double Co2 { get; init; }

    public double Pm25 { get; init; }

    public double Humidity { get; init; }
}
