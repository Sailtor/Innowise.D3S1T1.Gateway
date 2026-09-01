using Gateway.Domain.Entities;

namespace Gateway.Presentation.Types;

public class AirQualityReadingType : ObjectType<AirQualityReading>
{
    protected override void Configure(IObjectTypeDescriptor<AirQualityReading> descriptor)
    {
        descriptor.Name("AirQualityReading");
        descriptor.Description("Air quality recorded for a room.");
        descriptor.BindFieldsExplicitly();
        descriptor.Implements<MetricReadingInterfaceType>();

        MetricReadingFields.ConfigureShared(descriptor);

        descriptor.Field(r => r.Co2).Description("Carbon dioxide concentration.");
        descriptor.Field(r => r.Pm25).Description("Particulate matter under 2.5 micrometres.");
        descriptor.Field(r => r.Humidity).Description("Relative humidity.");
    }
}
