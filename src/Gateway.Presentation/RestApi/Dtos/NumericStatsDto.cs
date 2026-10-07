using Gateway.Application.Models;

namespace Gateway.Presentation.RestApi.Dtos;

/// <summary>
/// The REST wire shape for <see cref="NumericStats"/>.
/// </summary>
/// <param name="Count">Number of readings in the bucket.</param>
/// <param name="Min">Smallest value in the bucket.</param>
/// <param name="Max">Largest value in the bucket.</param>
/// <param name="Average">Mean value in the bucket.</param>
/// <param name="Sum">Total of the values in the bucket.</param>
public sealed record NumericStatsDto(
    int Count,
    double? Min,
    double? Max,
    double? Average,
    double? Sum)
{
    /// <summary>
    /// Maps the Application-layer aggregate onto its REST shape.
    /// </summary>
    /// <param name="stats">The stats to map.</param>
    /// <returns>The equivalent <see cref="NumericStatsDto"/>.</returns>
    public static NumericStatsDto FromDomain(NumericStats stats)
        => new(stats.Count, stats.Min, stats.Max, stats.Average, stats.Sum);
}
