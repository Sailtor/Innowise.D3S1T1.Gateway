using Gateway.Application.Interfaces;
using Gateway.Application.Models;
using Gateway.Domain.Entities;
using Gateway.Domain.Enums;
using Gateway.Infrastructure.Persistence;
using Gateway.Presentation.Types;
using Gateway.Presentation.Types.Filters;
using Gateway.Presentation.Types.Sorting;

namespace Gateway.Presentation.Queries;

public class MetricsQuery
{
    /// <summary>
    /// Paged, filterable, sortable feed of raw readings.
    /// <para>
    /// The MetricsReadDbContext parameter is supplied by HotChocolate, not by constructor
    /// injection: RegisterDbContextFactory gives each resolver its own context from the pool and
    /// disposes it when the resolver completes. That is what makes parallel field execution safe -
    /// a context shared across resolvers would be a data race.
    /// </para>
    /// <para>
    /// No projection middleware here on purpose. TPH means one table, so there is no join to
    /// eliminate and every column is already on the row; adding projections would buy nothing and
    /// cost a fragile layer over a polymorphic type.
    /// </para>
    /// </summary>
    /// <param name="context">
    /// Read-only context supplied per resolver by HotChocolate's registered DbContext factory.
    /// </param>
    /// <returns>An IQueryable of metric readings.</returns>
    [UseOffsetPaging(typeof(MetricReadingInterfaceType), IncludeTotalCount = true)]
    [UseFiltering(typeof(MetricReadingFilterInputType))]
    [UseSorting(typeof(MetricReadingSortInputType))]
    public IQueryable<MetricReading> GetMetricReadings(MetricsReadDbContext context)
        => context.MetricReadings;

    /// <summary>
    /// Aggregates one numeric over an optional time and room grouping.
    /// </summary>
    /// <param name="input">What to aggregate and how to group it.</param>
    /// <param name="queryService">Dashboard query service.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>One bucket per group.</returns>
    public Task<IReadOnlyList<MetricAggregationBucket>> GetMetricAggregationAsync(
        MetricAggregationQuery input,
        IMetricReadingQueryService queryService,
        CancellationToken cancellationToken)
        => queryService.AggregateAsync(input, cancellationToken);

    /// <summary>
    /// The most recent reading for each room and reading type.
    /// </summary>
    /// <param name="queryService">Dashboard query service.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="rooms">Rooms to restrict to. Omit for all rooms.</param>
    /// <param name="types">Reading types to restrict to. Omit for all types.</param>
    /// <returns>At most one reading per room and type.</returns>
    public Task<IReadOnlyList<MetricReading>> GetLatestReadingsAsync(
        IMetricReadingQueryService queryService,
        CancellationToken cancellationToken,
        IReadOnlyList<string>? rooms = null,
        IReadOnlyList<MetricReadingType>? types = null)
        => queryService.GetLatestAsync(rooms, types, cancellationToken);

    /// <summary>
    /// Per-room totals together with that room's latest readings, for the dashboard header.
    /// </summary>
    /// <param name="queryService">Dashboard query service.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>One summary per room that has produced at least one reading.</returns>
    public Task<IReadOnlyList<RoomSummary>> GetRoomsAsync(
        IMetricReadingQueryService queryService,
        CancellationToken cancellationToken)
        => queryService.GetRoomSummariesAsync(cancellationToken);

    /// <summary>
    /// Distinct room names, for populating client-side filter controls.
    /// </summary>
    /// <param name="queryService">Dashboard query service.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Distinct room names in alphabetical order.</returns>
    public Task<IReadOnlyList<string>> GetAvailableRoomsAsync(
        IMetricReadingQueryService queryService,
        CancellationToken cancellationToken)
        => queryService.GetRoomsAsync(cancellationToken);
}
