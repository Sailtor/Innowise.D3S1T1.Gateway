using Gateway.Domain.Entities;

namespace Gateway.Presentation.Types;

/// <summary>
/// The fields every reading shares. An object type that implements an interface must declare the
/// interface's fields itself - nothing is inherited - so the shared set is configured from one
/// place to keep the interface and its three implementations from drifting apart.
/// </summary>
internal static class MetricReadingFields
{
    private const string IdDescription = "Opaque identifier of the reading.";

    private const string RoomDescription = "Location the reading came from.";

    private const string TypeDescription = "Which kind of reading this is.";

    private const string IngestedAtDescription =
        "When the ingestor pulled the batch from the upstream API. Pipeline time, not observation " +
        "time - the upstream API exposes no sensor timestamp.";

    private const string ReceivedAtDescription =
        "When the processor persisted the reading. This is the field time aggregations bucket on.";

    public static void ConfigureShared(IInterfaceTypeDescriptor<MetricReading> descriptor)
    {
        descriptor.Field(r => r.Id)
            .Type<NonNullType<IdType>>()
            .Description(IdDescription);

        descriptor.Field(r => r.Room)
            .Description(RoomDescription);

        descriptor.Field(r => r.ReadingType)
            .Name("type")
            .Type<NonNullType<MetricReadingTypeEnumType>>()
            .Description(TypeDescription);

        descriptor.Field(r => r.IngestedAtUtc)
            .Name("ingestedAt")
            .Description(IngestedAtDescription);

        descriptor.Field(r => r.ReceivedAtUtc)
            .Name("receivedAt")
            .Description(ReceivedAtDescription);
    }

    public static void ConfigureShared<T>(IObjectTypeDescriptor<T> descriptor)
        where T : MetricReading
    {
        descriptor.Field(r => r.Id)
            .Type<NonNullType<IdType>>()
            .Description(IdDescription);

        descriptor.Field(r => r.Room)
            .Description(RoomDescription);

        descriptor.Field(r => r.ReadingType)
            .Name("type")
            .Type<NonNullType<MetricReadingTypeEnumType>>()
            .Description(TypeDescription);

        descriptor.Field(r => r.IngestedAtUtc)
            .Name("ingestedAt")
            .Description(IngestedAtDescription);

        descriptor.Field(r => r.ReceivedAtUtc)
            .Name("receivedAt")
            .Description(ReceivedAtDescription);
    }
}
