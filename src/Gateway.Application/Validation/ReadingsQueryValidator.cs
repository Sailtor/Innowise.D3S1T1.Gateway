using FluentValidation;
using Gateway.Application.Models;

namespace Gateway.Application.Validation;

/// <summary>
/// Guards the paged-readings query against an unbounded or inverted request.
/// <para>
/// Failures surface as a FluentValidation ValidationException - the REST endpoint maps that to a
/// 400 ValidationProblem, the same way the aggregation query's validator already does for GraphQL
/// (there it is fanned out into GraphQL errors by <c>GraphQlErrorFilter</c> instead).
/// </para>
/// </summary>
public sealed class ReadingsQueryValidator : AbstractValidator<ReadingsQuery>
{
    /// <summary>
    /// Largest number of rows a single page may request. Matches
    /// <c>DependencyInjectionRegistration.MaxPageSize</c>, GraphQL's own ceiling.
    /// </summary>
    public const int MaxTake = 100;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReadingsQueryValidator"/> class.
    /// </summary>
    public ReadingsQueryValidator()
    {
        RuleFor(query => query.Take)
            .InclusiveBetween(1, MaxTake)
            .WithMessage($"'take' must be between 1 and {MaxTake}.");

        RuleFor(query => query.Skip)
            .GreaterThanOrEqualTo(0)
            .WithMessage("'skip' must not be negative.");

        RuleFor(query => query.ReceivedAtTo)
            .Must((query, to) => query.ReceivedAtFrom is not { } from || to is not { } upper || from <= upper)
            .WithMessage("'receivedAtTo' must not be earlier than 'receivedAtFrom'.");

        RuleFor(query => query.IngestedAtTo)
            .Must((query, to) => query.IngestedAtFrom is not { } from || to is not { } upper || from <= upper)
            .WithMessage("'ingestedAtTo' must not be earlier than 'ingestedAtFrom'.");
    }
}
