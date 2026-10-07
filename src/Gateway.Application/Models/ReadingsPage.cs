using Gateway.Domain.Entities;

namespace Gateway.Application.Models;

/// <summary>
/// One page of a filtered, sorted readings query.
/// </summary>
/// <param name="Items">The readings on this page, already sorted and sliced.</param>
/// <param name="TotalCount">The number of readings matching the filter, independent of paging.</param>
public sealed record ReadingsPage(
    IReadOnlyList<MetricReading> Items,
    int TotalCount);
