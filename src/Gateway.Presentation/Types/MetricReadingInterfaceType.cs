using Gateway.Domain.Entities;

namespace Gateway.Presentation.Types;

/// <summary>
/// The polymorphic root of the schema. MetricReading is an abstract CLR class, not a C# interface,
/// so HotChocolate will not infer an interface type for it - every field returning it must name
/// this type explicitly (see Query.GetMetricReadings) or it silently becomes an object type.
/// </summary>
public class MetricReadingInterfaceType : InterfaceType<MetricReading>
{
    protected override void Configure(IInterfaceTypeDescriptor<MetricReading> descriptor)
    {
        descriptor.Name("MetricReading");
        descriptor.Description("A single sensor reading recorded by the ingestion pipeline.");
        descriptor.BindFieldsExplicitly();

        MetricReadingFields.ConfigureShared(descriptor);
    }
}
