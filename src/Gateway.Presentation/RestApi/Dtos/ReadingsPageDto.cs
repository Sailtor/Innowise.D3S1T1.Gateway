namespace Gateway.Presentation.RestApi.Dtos;

/// <summary>
/// One page of readings, plus the total count of readings matching the filter.
/// </summary>
/// <param name="Items">The readings on this page.</param>
/// <param name="TotalCount">The number of readings matching the filter, independent of paging.</param>
public sealed record ReadingsPageDto(
    IReadOnlyList<ReadingDto> Items,
    int TotalCount);
