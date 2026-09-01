using Gateway.Application.Models;
using Gateway.Domain.Entities;
using Gateway.Domain.Enums;

namespace Gateway.Application.Interfaces;

/// <summary>
/// The dashboard queries that cannot be expressed as a plain IQueryable feed.
/// <para>
/// The paged raw-readings field is deliberately absent: it stays a resolver returning IQueryable so
/// HotChocolate's filter, sort and paging middleware can push the whole thing into SQL. Everything
/// here instead opens and disposes its own short-lived context per call, which is what makes these
/// safe to execute in parallel as sibling root fields.
/// </para>
/// </summary>
public interface IMetricReadingQueryService
{
    /// <summary>
    /// Aggregates one numeric over an optional time and room grouping.
    /// </summary>
    /// <param name="query">What to aggregate and how to group it.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>One bucket per group, ordered by room then bucket start.</returns>
    Task<IReadOnlyList<MetricAggregationBucket>> AggregateAsync(
        MetricAggregationQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most recent reading for each room and reading type.
    /// </summary>
    /// <param name="rooms">Rooms to restrict to. Null or empty means all rooms.</param>
    /// <param name="types">Reading types to restrict to. Null or empty means all types.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>At most one reading per room and type.</returns>
    Task<IReadOnlyList<MetricReading>> GetLatestAsync(
        IReadOnlyCollection<string>? rooms,
        IReadOnlyCollection<MetricReadingType>? types,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets per-room totals together with that room's latest readings.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>One summary per room that has produced at least one reading.</returns>
    Task<IReadOnlyList<RoomSummary>> GetRoomSummariesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the distinct room names, for populating client-side filter controls.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Distinct room names in alphabetical order.</returns>
    Task<IReadOnlyList<string>> GetRoomsAsync(CancellationToken cancellationToken = default);
}
