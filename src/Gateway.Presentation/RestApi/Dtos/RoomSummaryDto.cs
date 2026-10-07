using Gateway.Application.Models;

namespace Gateway.Presentation.RestApi.Dtos;

/// <summary>
/// The REST wire shape for one room's dashboard summary.
/// </summary>
/// <param name="Room">The room.</param>
/// <param name="TotalReadings">How many readings this room has produced in total.</param>
/// <param name="LatestReading">The most recent reading of any type, or null if the room has none.</param>
/// <param name="LatestByType">The most recent reading of each type this room has produced.</param>
public sealed record RoomSummaryDto(
    string Room,
    int TotalReadings,
    ReadingDto? LatestReading,
    IReadOnlyList<ReadingDto> LatestByType)
{
    /// <summary>
    /// Maps the Application-layer summary onto its REST shape.
    /// </summary>
    /// <param name="summary">The summary to map.</param>
    /// <returns>The equivalent <see cref="RoomSummaryDto"/>.</returns>
    public static RoomSummaryDto FromDomain(RoomSummary summary)
        => new(
            summary.Room,
            summary.TotalReadings,
            summary.LatestReading is { } latest ? ReadingDto.FromDomain(latest) : null,
            [.. summary.LatestByType.Select(ReadingDto.FromDomain)]);
}
