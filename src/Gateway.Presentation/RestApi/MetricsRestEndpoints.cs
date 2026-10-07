using FluentValidation;
using FluentValidation.Results;
using Gateway.Application.Enums;
using Gateway.Application.Interfaces;
using Gateway.Application.Models;
using Gateway.Application.Validation;
using Gateway.Domain.Entities;
using Gateway.Domain.Enums;
using Gateway.Presentation.RestApi.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Gateway.Presentation.RestApi;

/// <summary>
/// The REST counterpart of the GraphQL query surface under <c>/api</c> - five read-only endpoints,
/// each delegating to the same <see cref="IMetricReadingQueryService"/> GraphQL's resolvers use.
/// <para>
/// Minimal API endpoints rather than MVC controllers: nothing else in this service calls
/// <c>AddControllers()</c>, and testing controllers through <c>WebApplicationFactory</c> would hit the
/// same <c>Log.CloseAndFlushAsync()</c> conflict GraphQL's own tests already avoid by not using it
/// (see <c>data/Gateway.md</c> gotcha 8) - Minimal API delegates can be invoked directly instead.
/// </para>
/// </summary>
public static class MetricsRestEndpoints
{
    /// <summary>
    /// Maps every REST endpoint under <c>/api</c>.
    /// </summary>
    /// <param name="app">The endpoint route builder to map onto.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapMetricsRestApi(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api").WithTags("Metrics");

        group.MapGet("/readings", GetReadingsAsync);
        group.MapGet("/readings/latest", GetLatestReadingsAsync);
        group.MapGet("/aggregations", GetAggregationsAsync);
        group.MapGet("/rooms", GetRoomsAsync);
        group.MapGet("/rooms/available", GetAvailableRoomsAsync);

        return app;
    }

    private static async Task<IResult> GetReadingsAsync(
        IMetricReadingQueryService queryService,
        CancellationToken cancellationToken,
        string? room = null,
        string? type = null,
        DateTime? receivedAtFrom = null,
        DateTime? receivedAtTo = null,
        DateTime? ingestedAtFrom = null,
        DateTime? ingestedAtTo = null,
        string? sortBy = null,
        string? sortDir = null,
        int? skip = null,
        int? take = null)
    {
        if (!TryParseEnum(type, "type", out MetricReadingType? parsedType, out IResult? typeError))
        {
            return typeError!;
        }

        if (!TryParseEnum(sortBy, "sortBy", out ReadingSortField? parsedSortBy, out IResult? sortByError))
        {
            return sortByError!;
        }

        if (!TryParseSortDirection(sortDir, out SortDirection? parsedSortDir, out IResult? sortDirError))
        {
            return sortDirError!;
        }

        // Built from ReadingsQuery's own property-initialiser defaults rather than redeclaring
        // ReceivedAt/Descending/20 here too - one source of truth for "what a caller gets when they
        // don't specify a sort/page".
        ReadingsQuery query = new()
        {
            Room = room,
            Type = parsedType,
            ReceivedAtFrom = receivedAtFrom,
            ReceivedAtTo = receivedAtTo,
            IngestedAtFrom = ingestedAtFrom,
            IngestedAtTo = ingestedAtTo,
        };

        if (parsedSortBy is { } sortByValue)
        {
            query = query with { SortBy = sortByValue };
        }

        if (parsedSortDir is { } sortDirValue)
        {
            query = query with { SortDir = sortDirValue };
        }

        if (skip is { } skipValue)
        {
            query = query with { Skip = skipValue };
        }

        if (take is { } takeValue)
        {
            query = query with { Take = takeValue };
        }

        try
        {
            ReadingsPage page = await queryService.GetReadingsAsync(query, cancellationToken);

            return Results.Ok(new ReadingsPageDto(
                [.. page.Items.Select(ReadingDto.FromDomain)],
                page.TotalCount));
        }
        catch (ValidationException exception)
        {
            return ToValidationProblem(exception);
        }
    }

    private static async Task<IResult> GetLatestReadingsAsync(
        IMetricReadingQueryService queryService,
        CancellationToken cancellationToken,
        string? rooms = null,
        string? types = null)
    {
        if (!TryParseEnumList(types, "types", out IReadOnlyList<MetricReadingType>? parsedTypes, out IResult? error))
        {
            return error!;
        }

        IReadOnlyList<MetricReading> latest = await queryService.GetLatestAsync(
            SplitList(rooms), parsedTypes, cancellationToken);

        return Results.Ok(latest.Select(ReadingDto.FromDomain).ToArray());
    }

    private static async Task<IResult> GetAggregationsAsync(
        IMetricReadingQueryService queryService,
        CancellationToken cancellationToken,
        string? field = null,
        DateTime? from = null,
        DateTime? to = null,
        string? rooms = null,
        bool groupByRoom = false,
        string? interval = null)
    {
        if (field is null)
        {
            return InvalidParameter("field", "'field' is required.");
        }

        if (!TryParseEnum(field, "field", out AggregationField? parsedFieldOrNull, out IResult? fieldError))
        {
            return fieldError!;
        }

        AggregationField parsedField = parsedFieldOrNull!.Value;

        if (!TryParseEnum(interval, "interval", out TimeInterval? parsedInterval, out IResult? intervalError))
        {
            return intervalError!;
        }

        MetricAggregationQuery query = new()
        {
            Field = parsedField,
            From = from,
            To = to,
            Rooms = SplitList(rooms),
            GroupByRoom = groupByRoom,
            Interval = parsedInterval,
        };

        try
        {
            IReadOnlyList<MetricAggregationBucket> buckets =
                await queryService.AggregateAsync(query, cancellationToken);

            return Results.Ok(buckets.Select(AggregationBucketDto.FromDomain).ToArray());
        }
        catch (ValidationException exception)
        {
            return ToValidationProblem(exception);
        }
    }

    private static async Task<IResult> GetRoomsAsync(
        IMetricReadingQueryService queryService,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<RoomSummary> summaries = await queryService.GetRoomSummariesAsync(cancellationToken);

        return Results.Ok(summaries.Select(RoomSummaryDto.FromDomain).ToArray());
    }

    private static async Task<IResult> GetAvailableRoomsAsync(
        IMetricReadingQueryService queryService,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> rooms = await queryService.GetRoomsAsync(cancellationToken);

        return Results.Ok(rooms);
    }

    private static string[]? SplitList(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Parses an optional query-string value as an enum, producing the same ValidationProblem shape a
    /// failed <see cref="ReadingsQueryValidator"/>/<see cref="MetricAggregationQueryValidator"/> run
    /// produces - one error shape for every "a query parameter was malformed" case, not two.
    /// </summary>
    private static bool TryParseEnum<TEnum>(string? value, string parameterName, out TEnum? parsed, out IResult? error)
        where TEnum : struct, Enum
    {
        if (value is null)
        {
            parsed = null;
            error = null;
            return true;
        }

        if (Enum.TryParse(value, ignoreCase: true, out TEnum result))
        {
            parsed = result;
            error = null;
            return true;
        }

        parsed = null;
        error = InvalidParameter(parameterName, $"'{value}' is not a valid {typeof(TEnum).Name}.");
        return false;
    }

    /// <summary>
    /// Parses the wire-level <c>asc</c>/<c>desc</c> spelling of a sort direction. Deliberately not
    /// <see cref="TryParseEnum{TEnum}"/>: the documented query-string contract (<c>asc</c>/<c>desc</c>)
    /// is shorter than <see cref="SortDirection"/>'s own member names, so it needs its own mapping
    /// rather than requiring callers to spell out "Ascending"/"Descending".
    /// </summary>
    private static bool TryParseSortDirection(string? value, out SortDirection? parsed, out IResult? error)
    {
        switch (value)
        {
            case null:
                parsed = null;
                error = null;
                return true;
            case not null when string.Equals(value, "asc", StringComparison.OrdinalIgnoreCase):
                parsed = SortDirection.Ascending;
                error = null;
                return true;
            case not null when string.Equals(value, "desc", StringComparison.OrdinalIgnoreCase):
                parsed = SortDirection.Descending;
                error = null;
                return true;
            default:
                parsed = null;
                error = InvalidParameter("sortDir", $"'{value}' is not a valid sort direction. Use 'asc' or 'desc'.");
                return false;
        }
    }

    private static bool TryParseEnumList<TEnum>(
        string? value,
        string parameterName,
        out IReadOnlyList<TEnum>? parsed,
        out IResult? error)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            parsed = null;
            error = null;
            return true;
        }

        string[] parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        List<TEnum> result = new(parts.Length);

        foreach (string part in parts)
        {
            if (!Enum.TryParse(part, ignoreCase: true, out TEnum item))
            {
                parsed = null;
                error = InvalidParameter(parameterName, $"'{part}' is not a valid {typeof(TEnum).Name}.");
                return false;
            }

            result.Add(item);
        }

        parsed = result;
        error = null;
        return true;
    }

    private static IResult InvalidParameter(string parameterName, string message)
        => Results.ValidationProblem(new Dictionary<string, string[]> { [parameterName] = [message] });

    private static IResult ToValidationProblem(ValidationException exception)
    {
        Dictionary<string, string[]> errors = exception.Errors
            .GroupBy(static (ValidationFailure failure) => failure.PropertyName)
            .ToDictionary(
                static group => group.Key,
                static group => group.Select(failure => failure.ErrorMessage).ToArray());

        return Results.ValidationProblem(errors);
    }
}
