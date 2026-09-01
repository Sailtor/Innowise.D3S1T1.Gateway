using FluentValidation;
using Gateway.Application.Enums;
using Gateway.Application.Models;

namespace Gateway.Application.Validation;

/// <summary>
/// Guards the aggregation query against bucket-count explosions.
/// <para>
/// A five-year window at MINUTE resolution is 2.6 million buckets: the database will happily
/// compute it and the response will take the process down. These limits are the difference between
/// a slow query and a denial of service.
/// </para>
/// <para>
/// Failures surface as a FluentValidation ValidationException, which the presentation layer's error
/// filter fans out into one GraphQL error per failure, each carrying a VALIDATION_FAILED code and
/// the offending field name.
/// </para>
/// </summary>
public sealed class MetricAggregationQueryValidator : AbstractValidator<MetricAggregationQuery>
{
    /// <summary>
    /// Largest number of rooms a single query may name.
    /// </summary>
    public const int MaxRooms = 50;

    private static readonly TimeSpan MaxMinuteWindow = TimeSpan.FromHours(24);

    private static readonly TimeSpan MaxHourWindow = TimeSpan.FromDays(90);

    /// <summary>
    /// Initializes a new instance of the <see cref="MetricAggregationQueryValidator"/> class.
    /// </summary>
    public MetricAggregationQueryValidator()
    {
        RuleFor(query => query.To)
            .Must((query, to) => query.From is not { } from || to is not { } upper || from <= upper)
            .WithMessage("'to' must not be earlier than 'from'.");

        RuleFor(query => query.Rooms)
            .Must(rooms => rooms is null || rooms.Count <= MaxRooms)
            .WithMessage($"At most {MaxRooms} rooms may be requested at once.");

        RuleFor(query => query.Interval)
            .Must((query, _) => IsWindowBounded(query))
            .When(query => query.Interval is TimeInterval.Minute or TimeInterval.Hour)
            .WithMessage("A MINUTE or HOUR interval requires both 'from' and 'to', so the number of buckets is bounded.");

        RuleFor(query => query.Interval)
            .Must((query, _) => IsWindowWithin(query, MaxMinuteWindow))
            .When(query => query.Interval is TimeInterval.Minute && IsWindowBounded(query))
            .WithMessage($"A MINUTE interval supports a window of at most {MaxMinuteWindow.TotalHours:0} hours.");

        RuleFor(query => query.Interval)
            .Must((query, _) => IsWindowWithin(query, MaxHourWindow))
            .When(query => query.Interval is TimeInterval.Hour && IsWindowBounded(query))
            .WithMessage($"An HOUR interval supports a window of at most {MaxHourWindow.TotalDays:0} days.");
    }

    private static bool IsWindowBounded(MetricAggregationQuery query)
        => query.From.HasValue && query.To.HasValue;

    private static bool IsWindowWithin(MetricAggregationQuery query, TimeSpan maximum)
        => !IsWindowBounded(query) || query.To!.Value - query.From!.Value <= maximum;
}
