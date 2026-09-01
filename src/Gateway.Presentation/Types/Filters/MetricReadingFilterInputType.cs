using Gateway.Domain.Entities;
using HotChocolate.Data.Filters;

namespace Gateway.Presentation.Types.Filters;

/// <summary>
/// Explicit allowlist rather than HotChocolate's default implicit binding. Auto-binding every
/// property produces a sprawling filter surface and lets a client compose predicates the database
/// has no index for; naming the four dimensions the dashboard actually filters on is what makes
/// "strongly-typed schema" mean something. Every field here is covered by an index or is the
/// discriminator column.
/// </summary>
public class MetricReadingFilterInputType : FilterInputType<MetricReading>
{
    protected override void Configure(IFilterInputTypeDescriptor<MetricReading> descriptor)
    {
        descriptor.Name("MetricReadingFilterInput");
        descriptor.BindFieldsExplicitly();

        descriptor.Field(r => r.Room);
        descriptor.Field(r => r.ReadingType).Name("type");
        descriptor.Field(r => r.ReceivedAtUtc).Name("receivedAt");
        descriptor.Field(r => r.IngestedAtUtc).Name("ingestedAt");
    }
}
