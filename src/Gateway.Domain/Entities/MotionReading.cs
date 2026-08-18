namespace Gateway.Domain.Entities;

public sealed class MotionReading : MetricReading
{
    public bool IsMotionDetected { get; init; }
}
