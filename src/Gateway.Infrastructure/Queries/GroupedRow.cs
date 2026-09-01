namespace Gateway.Infrastructure.Queries;

/// <summary>
/// The materialised shape of one aggregated group, before the bucket ordinal is turned back into a
/// DateTime on the client.
/// </summary>
internal sealed class GroupedRow
{
    public string? Room { get; init; }

    public int? Bucket { get; init; }

    public int Count { get; init; }

    public double? Min { get; init; }

    public double? Max { get; init; }

    public double? Average { get; init; }

    public double? Sum { get; init; }
}
