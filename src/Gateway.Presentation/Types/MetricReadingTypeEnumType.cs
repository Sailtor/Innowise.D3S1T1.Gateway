using Gateway.Domain.Enums;

namespace Gateway.Presentation.Types;

/// <summary>
/// GraphQL enum for the reading discriminator. `None` exists only to mirror the writer's enum and
/// is never persisted, so it is hidden rather than offered to clients as a meaningless choice.
/// Members surface as ENERGY / AIR_QUALITY / MOTION via HotChocolate's default enum naming.
/// </summary>
public class MetricReadingTypeEnumType : EnumType<MetricReadingType>
{
    protected override void Configure(IEnumTypeDescriptor<MetricReadingType> descriptor)
    {
        descriptor.Name("MetricReadingType");
        descriptor.Ignore(MetricReadingType.None);
    }
}
