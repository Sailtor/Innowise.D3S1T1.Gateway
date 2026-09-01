using Gateway.Domain.Entities;

namespace Gateway.Presentation.Types;

public class EnergyReadingType : ObjectType<EnergyReading>
{
    protected override void Configure(IObjectTypeDescriptor<EnergyReading> descriptor)
    {
        descriptor.Name("EnergyReading");
        descriptor.Description("Energy consumption recorded for a room.");
        descriptor.BindFieldsExplicitly();
        descriptor.Implements<MetricReadingInterfaceType>();

        MetricReadingFields.ConfigureShared(descriptor);

        descriptor.Field(r => r.EnergyAmount)
            .Description("Energy consumed, in the unit reported by the upstream API.");
    }
}
