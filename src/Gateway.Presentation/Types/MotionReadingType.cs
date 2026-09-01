using Gateway.Domain.Entities;

namespace Gateway.Presentation.Types;

public class MotionReadingType : ObjectType<MotionReading>
{
    protected override void Configure(IObjectTypeDescriptor<MotionReading> descriptor)
    {
        descriptor.Name("MotionReading");
        descriptor.Description("Motion detection recorded for a room.");
        descriptor.BindFieldsExplicitly();
        descriptor.Implements<MetricReadingInterfaceType>();

        MetricReadingFields.ConfigureShared(descriptor);

        descriptor.Field(r => r.IsMotionDetected).Description("Whether motion was detected.");
    }
}
