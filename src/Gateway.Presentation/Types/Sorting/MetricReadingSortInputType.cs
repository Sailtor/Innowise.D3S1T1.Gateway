using Gateway.Domain.Entities;
using HotChocolate.Data.Sorting;

namespace Gateway.Presentation.Types.Sorting;

/// <summary>
/// Same reasoning as the filter type: an explicit allowlist, restricted to columns an index covers.
/// </summary>
public class MetricReadingSortInputType : SortInputType<MetricReading>
{
    protected override void Configure(ISortInputTypeDescriptor<MetricReading> descriptor)
    {
        descriptor.Name("MetricReadingSortInput");
        descriptor.BindFieldsExplicitly();

        descriptor.Field(r => r.ReceivedAtUtc).Name("receivedAt");
        descriptor.Field(r => r.IngestedAtUtc).Name("ingestedAt");
        descriptor.Field(r => r.Room);
        descriptor.Field(r => r.ReadingType).Name("type");
    }
}
