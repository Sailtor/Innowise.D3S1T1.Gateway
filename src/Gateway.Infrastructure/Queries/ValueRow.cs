namespace Gateway.Infrastructure.Queries;

/// <summary>
/// One reading flattened to the single numeric being aggregated. Projecting to this shape first is
/// what lets the aggregate lambdas be literals: EF cannot translate g.Min(someExpression) where the
/// selector is a captured variable rather than an inline lambda.
/// </summary>
internal sealed class ValueRow
{
    public string Room { get; init; } = string.Empty;

    public DateTime ReceivedAtUtc { get; init; }

    public double Value { get; init; }
}
