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
/// Plain argument exceptions for now. Phase 5 introduces FluentValidation together with the error
/// filter that maps validation failures to a VALIDATION_FAILED GraphQL code - the two belong in one
/// change, because until the filter exists a validation failure would reach the client masked as an
/// unexpected internal error, which is worse than the message below.
/// </para>
/// </summary>
public static class MetricAggregationQueryValidator
{
    /// <summary>
    /// Largest number of rooms a single query may name.
    /// </summary>
    public const int MaxRooms = 50;

    private static readonly TimeSpan MaxMinuteWindow = TimeSpan.FromHours(24);

    private static readonly TimeSpan MaxHourWindow = TimeSpan.FromDays(90);

    /// <summary>
    /// Throws if the query would be unbounded or would produce an unreasonable number of buckets.
    /// </summary>
    /// <param name="query">The query to check.</param>
    public static void Validate(MetricAggregationQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.From is { } from && query.To is { } to && from > to)
        {
            throw new ArgumentException("'from' must not be later than 'to'.", nameof(query));
        }

        if (query.Rooms is { Count: > MaxRooms })
        {
            throw new ArgumentException(
                $"At most {MaxRooms} rooms may be requested at once; got {query.Rooms.Count}.",
                nameof(query));
        }

        if (query.Interval is TimeInterval.Minute)
        {
            EnsureWindowWithin(query, MaxMinuteWindow, "MINUTE");
        }

        if (query.Interval is TimeInterval.Hour)
        {
            EnsureWindowWithin(query, MaxHourWindow, "HOUR");
        }
    }

    private static void EnsureWindowWithin(MetricAggregationQuery query, TimeSpan maximum, string intervalName)
    {
        if (query.From is not { } from || query.To is not { } to)
        {
            throw new ArgumentException(
                $"A {intervalName} interval requires both 'from' and 'to', so the number of buckets is bounded.",
                nameof(query));
        }

        if (to - from > maximum)
        {
            throw new ArgumentException(
                $"A {intervalName} interval supports a window of at most {maximum.TotalHours:0} hours; got {(to - from).TotalHours:0}.",
                nameof(query));
        }
    }
}
