using Gateway.Application.Enums;
using Gateway.Application.Models;
using Gateway.Application.Validation;

namespace Gateway.Application.Tests;

public class MetricAggregationQueryValidatorTests
{
    private static readonly DateTime From = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void AcceptsAnUngroupedUnboundedQuery()
    {
        MetricAggregationQueryValidator.Validate(new MetricAggregationQuery
        {
            Field = AggregationField.EnergyAmount,
        });
    }

    [Fact]
    public void RejectsAnInvertedWindow()
    {
        MetricAggregationQuery query = new()
        {
            Field = AggregationField.Co2,
            From = From,
            To = From.AddHours(-1),
        };

        Assert.Throws<ArgumentException>(() => MetricAggregationQueryValidator.Validate(query));
    }

    [Fact]
    public void RejectsMoreRoomsThanTheLimit()
    {
        MetricAggregationQuery query = new()
        {
            Field = AggregationField.Co2,
            Rooms = [.. Enumerable.Range(0, MetricAggregationQueryValidator.MaxRooms + 1).Select(i => $"room-{i}")],
        };

        Assert.Throws<ArgumentException>(() => MetricAggregationQueryValidator.Validate(query));
    }

    [Theory]
    [InlineData(TimeInterval.Minute)]
    [InlineData(TimeInterval.Hour)]
    public void RejectsAFineIntervalWithoutABoundedWindow(TimeInterval interval)
    {
        // Unbounded plus fine-grained is the bucket explosion the limits exist to prevent.
        MetricAggregationQuery query = new()
        {
            Field = AggregationField.EnergyAmount,
            Interval = interval,
        };

        Assert.Throws<ArgumentException>(() => MetricAggregationQueryValidator.Validate(query));
    }

    [Fact]
    public void RejectsMinuteBucketsOverMoreThanADay()
    {
        MetricAggregationQuery query = new()
        {
            Field = AggregationField.EnergyAmount,
            From = From,
            To = From.AddHours(25),
            Interval = TimeInterval.Minute,
        };

        Assert.Throws<ArgumentException>(() => MetricAggregationQueryValidator.Validate(query));
    }

    [Fact]
    public void AcceptsMinuteBucketsWithinADay()
    {
        MetricAggregationQueryValidator.Validate(new MetricAggregationQuery
        {
            Field = AggregationField.EnergyAmount,
            From = From,
            To = From.AddHours(24),
            Interval = TimeInterval.Minute,
        });
    }

    [Fact]
    public void RejectsHourBucketsOverMoreThanNinetyDays()
    {
        MetricAggregationQuery query = new()
        {
            Field = AggregationField.Humidity,
            From = From,
            To = From.AddDays(91),
            Interval = TimeInterval.Hour,
        };

        Assert.Throws<ArgumentException>(() => MetricAggregationQueryValidator.Validate(query));
    }

    [Fact]
    public void AcceptsDayBucketsOverAnUnboundedWindow()
    {
        // Daily buckets cannot explode: even a decade is a few thousand rows.
        MetricAggregationQueryValidator.Validate(new MetricAggregationQuery
        {
            Field = AggregationField.MotionDetected,
            Interval = TimeInterval.Day,
        });
    }
}
