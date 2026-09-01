using Gateway.Domain.Entities;

namespace Gateway.Application.Models;

/// <summary>
/// Dashboard header data for one room. Fully materialised by the query service rather than
/// resolved field-by-field, so there is no N+1 to batch away with a DataLoader.
/// </summary>
/// <param name="Room">The room.</param>
/// <param name="TotalReadings">How many readings this room has produced in total.</param>
/// <param name="LatestReading">The most recent reading of any type, or null if the room has none.</param>
/// <param name="LatestByType">The most recent reading of each type this room has produced.</param>
public sealed record RoomSummary(
    string Room,
    int TotalReadings,
    MetricReading? LatestReading,
    IReadOnlyList<MetricReading> LatestByType);
