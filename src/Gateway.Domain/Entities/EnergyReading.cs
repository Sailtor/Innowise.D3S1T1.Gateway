namespace Gateway.Domain.Entities;

public sealed class EnergyReading : MetricReading
{
    public double EnergyAmount { get; init; }
}
