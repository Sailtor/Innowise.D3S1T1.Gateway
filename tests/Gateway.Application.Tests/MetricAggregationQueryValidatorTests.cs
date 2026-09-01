using FluentValidation.Results;
using Gateway.Application.Enums;
using Gateway.Application.Models;
using Gateway.Application.Validation;

namespace Gateway.Application.Tests;

public class MetricAggregationQueryValidatorTests
{
    private static readonly DateTime From = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly MetricAggregationQueryValidator validator = new();

    [Fact]
    public void AcceptsAnUngroupedUnboundedQuery()
    {
        ValidationResult result = validator.Validate(new MetricAggregationQuery
        {
            Field = AggregationField.EnergyAmount,
        });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void RejectsAnInvertedWindow()
    {
        ValidationResult result = validator.Validate(new MetricAggregationQuery
        {
            Field = AggregationField.Co2,
            From = From,
            To = From.AddHours(-1),
        });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void RejectsMoreRoomsThanTheLimit()
    {
        ValidationResult result = validator.Validate(new MetricAggregationQuery
        {
            Field = AggregationField.Co2,
            Rooms = [.. Enumerable.Range(0, MetricAggregationQueryValidator.MaxRooms + 1).Select(i => $"room-{i}")],
        });

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(TimeInterval.Minute)]
    [InlineData(TimeInterval.Hour)]
    public void RejectsAFineIntervalWithoutABoundedWindow(TimeInterval interval)
    {
        // Unbounded plus fine-grained is the bucket explosion the limits exist to prevent.
        ValidationResult result = validator.Validate(new MetricAggregationQuery
        {
            Field = AggregationField.EnergyAmount,
            Interval = interval,
        });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void RejectsMinuteBucketsOverMoreThanADay()
    {
        ValidationResult result = validator.Validate(new MetricAggregationQuery
        {
            Field = AggregationField.EnergyAmount,
            From = From,
            To = From.AddHours(25),
            Interval = TimeInterval.Minute,
        });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void AcceptsMinuteBucketsWithinADay()
    {
        ValidationResult result = validator.Validate(new MetricAggregationQuery
        {
            Field = AggregationField.EnergyAmount,
            From = From,
            To = From.AddHours(24),
            Interval = TimeInterval.Minute,
        });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void RejectsHourBucketsOverMoreThanNinetyDays()
    {
        ValidationResult result = validator.Validate(new MetricAggregationQuery
        {
            Field = AggregationField.Humidity,
            From = From,
            To = From.AddDays(91),
            Interval = TimeInterval.Hour,
        });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void AcceptsDayBucketsOverAnUnboundedWindow()
    {
        // Daily buckets cannot explode: even a decade is a few thousand rows.
        ValidationResult result = validator.Validate(new MetricAggregationQuery
        {
            Field = AggregationField.MotionDetected,
            Interval = TimeInterval.Day,
        });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ReportsTheOffendingFieldSoTheClientCanRenderItInline()
    {
        ValidationResult result = validator.Validate(new MetricAggregationQuery
        {
            Field = AggregationField.Co2,
            From = From,
            To = From.AddHours(-1),
        });

        Assert.All(result.Errors, failure => Assert.False(string.IsNullOrWhiteSpace(failure.PropertyName)));
    }
}
