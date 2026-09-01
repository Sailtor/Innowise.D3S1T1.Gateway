namespace Gateway.Infrastructure.Queries;

/// <summary>
/// A <see cref="ValueRow"/> with its time bucket resolved to an ordinal offset from the epoch.
/// Bucketing in a projection rather than in the group key keeps the number of hand-written query
/// shapes down: one per interval here, rather than one per interval times one per grouping.
/// </summary>
internal sealed class BucketedRow
{
    public string Room { get; init; } = string.Empty;

    public int Bucket { get; init; }

    public double Value { get; init; }
}
