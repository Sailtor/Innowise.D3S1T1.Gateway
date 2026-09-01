using Gateway.Application.Models;

namespace Gateway.Presentation.Types;

/// <summary>
/// Renames the Application-layer query record for the schema. Without this the input object would
/// be called MetricAggregationQueryInput, which leaks a C# type name into the public contract.
/// </summary>
public class MetricAggregationInputType : InputObjectType<MetricAggregationQuery>
{
    protected override void Configure(IInputObjectTypeDescriptor<MetricAggregationQuery> descriptor)
    {
        descriptor.Name("MetricAggregationInput");
        descriptor.Description("Which numeric to aggregate, over what slice, grouped how.");

        // Without an explicit default a non-nullable bool becomes `Boolean!` with no default, which
        // the spec makes mandatory - every client would have to pass groupByRoom even to ask for a
        // plain ungrouped total. HotChocolate cannot see the C# property initialiser.
        descriptor.Field(f => f.GroupByRoom).DefaultValue(false);
    }
}
