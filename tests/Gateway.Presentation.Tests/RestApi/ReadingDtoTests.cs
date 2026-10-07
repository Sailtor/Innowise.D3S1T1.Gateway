using Gateway.Domain.Entities;
using Gateway.Domain.Enums;
using Gateway.Presentation.RestApi.Dtos;

namespace Gateway.Presentation.Tests.RestApi;

/// <summary>
/// <see cref="ReadingDto.FromDomain"/> is the only place a TPH entity crosses onto the REST wire -
/// get a field wrong here and every other field-specific null stays silently empty instead.
/// </summary>
public class ReadingDtoTests
{
    private static readonly DateTime IngestedAt = new(2026, 8, 18, 10, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime ReceivedAt = new(2026, 8, 18, 10, 1, 0, DateTimeKind.Utc);

    [Fact]
    public void MapsAnEnergyReadingWithEveryOtherFieldNull()
    {
        EnergyReading energy = new()
        {
            Id = 1,
            Room = "kitchen",
            ReadingType = MetricReadingType.Energy,
            IngestedAtUtc = IngestedAt,
            ReceivedAtUtc = ReceivedAt,
            EnergyAmount = 2.5,
        };

        ReadingDto dto = ReadingDto.FromDomain(energy);

        Assert.Equal(1, dto.Id);
        Assert.Equal("kitchen", dto.Room);
        Assert.Equal(MetricReadingType.Energy, dto.Type);
        Assert.Equal(IngestedAt, dto.IngestedAt);
        Assert.Equal(ReceivedAt, dto.ReceivedAt);
        Assert.Equal(2.5, dto.EnergyAmount);
        Assert.Null(dto.Co2);
        Assert.Null(dto.Pm25);
        Assert.Null(dto.Humidity);
        Assert.Null(dto.IsMotionDetected);
    }

    [Fact]
    public void MapsAnAirQualityReadingWithEveryOtherFieldNull()
    {
        AirQualityReading airQuality = new()
        {
            Id = 2,
            Room = "office",
            ReadingType = MetricReadingType.AirQuality,
            IngestedAtUtc = IngestedAt,
            ReceivedAtUtc = ReceivedAt,
            Co2 = 600,
            Pm25 = 9,
            Humidity = 50,
        };

        ReadingDto dto = ReadingDto.FromDomain(airQuality);

        Assert.Equal(600, dto.Co2);
        Assert.Equal(9, dto.Pm25);
        Assert.Equal(50, dto.Humidity);
        Assert.Null(dto.EnergyAmount);
        Assert.Null(dto.IsMotionDetected);
    }

    [Fact]
    public void MapsAMotionReadingWithEveryOtherFieldNull()
    {
        MotionReading motion = new()
        {
            Id = 3,
            Room = "office",
            ReadingType = MetricReadingType.Motion,
            IngestedAtUtc = IngestedAt,
            ReceivedAtUtc = ReceivedAt,
            IsMotionDetected = true,
        };

        ReadingDto dto = ReadingDto.FromDomain(motion);

        Assert.True(dto.IsMotionDetected);
        Assert.Null(dto.EnergyAmount);
        Assert.Null(dto.Co2);
        Assert.Null(dto.Pm25);
        Assert.Null(dto.Humidity);
    }
}
