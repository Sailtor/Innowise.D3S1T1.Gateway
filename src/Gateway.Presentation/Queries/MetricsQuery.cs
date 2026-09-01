using Gateway.Domain.Entities;
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
}
