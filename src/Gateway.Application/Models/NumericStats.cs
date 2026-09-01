namespace Gateway.Application.Models;

/// <summary>
/// Aggregates over one bucket.
/// <para>
/// A grouped bucket only exists when it has at least one row, so in practice the nullable members
/// are populated; they are nullable to carry the synthesised zero-row bucket that an ungrouped
/// query returns when the window is empty.
/// </para>
/// </summary>
/// <param name="Count">Number of readings in the bucket.</param>
/// <param name="Min">Smallest value in the bucket.</param>
/// <param name="Max">Largest value in the bucket.</param>
/// <param name="Average">Mean value in the bucket.</param>
/// <param name="Sum">Total of the values in the bucket.</param>
public sealed record NumericStats(
    int Count,
    double? Min,
    double? Max,
    double? Average,
    double? Sum);
