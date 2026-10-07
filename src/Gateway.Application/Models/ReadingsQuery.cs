using Gateway.Application.Enums;
using Gateway.Domain.Enums;

namespace Gateway.Application.Models;

/// <summary>
/// A paged, filtered, sorted slice of raw readings - the REST counterpart of GraphQL's
/// <c>metricReadings</c> field.
/// <para>
/// GraphQL gets this shape for free from HotChocolate's filtering/sorting/paging middleware
/// composing directly onto an <see cref="IQueryable{T}"/>; REST has no such middleware, so this
/// query is built and executed by hand in <c>MetricReadingQueryService.GetReadingsAsync</c>. The
/// four filterable fields and four sortable fields mirror
/// <c>MetricReadingFilterInputType</c>/<c>MetricReadingSortInputType</c>'s allowlists exactly -
/// this is not a wider surface than GraphQL already exposes.
/// </para>
/// </summary>
public sealed record ReadingsQuery
{
    /// <summary>
    /// Gets the room to restrict to. Null means every room.
    /// </summary>
    public string? Room { get; init; }

    /// <summary>
    /// Gets the reading type to restrict to. Null means every type.
    /// </summary>
    public MetricReadingType? Type { get; init; }

    /// <summary>
    /// Gets the inclusive lower bound on ReceivedAtUtc.
    /// </summary>
    public DateTime? ReceivedAtFrom { get; init; }

    /// <summary>
    /// Gets the exclusive upper bound on ReceivedAtUtc.
    /// </summary>
    public DateTime? ReceivedAtTo { get; init; }

    /// <summary>
    /// Gets the inclusive lower bound on IngestedAtUtc.
    /// </summary>
    public DateTime? IngestedAtFrom { get; init; }

    /// <summary>
    /// Gets the exclusive upper bound on IngestedAtUtc.
    /// </summary>
    public DateTime? IngestedAtTo { get; init; }

    /// <summary>
    /// Gets the column to sort by.
    /// </summary>
    public ReadingSortField SortBy { get; init; } = ReadingSortField.ReceivedAt;

    /// <summary>
    /// Gets the sort direction.
    /// </summary>
    public SortDirection SortDir { get; init; } = SortDirection.Descending;

    /// <summary>
    /// Gets the number of rows to skip.
    /// </summary>
    public int Skip { get; init; }

    /// <summary>
    /// Gets the number of rows to return.
    /// </summary>
    public int Take { get; init; } = 20;
}
