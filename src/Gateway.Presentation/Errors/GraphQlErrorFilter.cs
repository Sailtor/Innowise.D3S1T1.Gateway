using FluentValidation;
using FluentValidation.Results;
using HotChocolate.Execution;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Gateway.Presentation.Errors;

/// <summary>
/// Turns exceptions escaping a resolver into GraphQL errors a client can act on.
/// <para>
/// Validation failures become one error per broken rule, each carrying the field name so the
/// frontend can render the message inline. Everything else is logged in full and then masked
/// outside Development - an unhandled exception message can carry SQL, a table name or a
/// connection string, none of which belong in a client response.
/// </para>
/// <para>
/// Note this type is constructed by hand in the registration rather than by the container. Error
/// filters are activated from HotChocolate's schema service provider, which is a separate
/// ServiceCollection with no logging and no hosting services in it, so constructor injection would
/// fail when the executor is built.
/// </para>
/// </summary>
internal sealed class GraphQlErrorFilter(
    ILogger<GraphQlErrorFilter> logger,
    IHostEnvironment environment) : IErrorFilter
{
    private const string ValidationFailedCode = "VALIDATION_FAILED";

    private const string InternalErrorCode = "INTERNAL_SERVER_ERROR";

    private const string MaskedMessage = "An unexpected error occurred while executing the request.";

    /// <summary>
    /// Rewrites one error on its way out of the execution engine.
    /// </summary>
    /// <param name="error">The error as the engine produced it.</param>
    /// <returns>The error to return to the client.</returns>
    public IError OnError(IError error)
    {
        // Syntax, validation and other document errors carry no exception. They are already safe
        // and already meaningful, so they pass through untouched.
        if (error.Exception is null)
        {
            return error;
        }

        if (error.Exception is ValidationException validation)
        {
            return ToValidationErrors(error, validation);
        }

        logger.LogError(
            error.Exception,
            "Unhandled exception resolving GraphQL field at path {Path}",
            error.Path?.ToString() ?? "<unknown>");

        if (environment.IsDevelopment())
        {
            return ErrorBuilder.FromError(error).SetCode(InternalErrorCode).Build();
        }

        return ErrorBuilder.FromError(error)
            .SetMessage(MaskedMessage)
            .SetCode(InternalErrorCode)
            .SetException(null)
            .Build();
    }

    /// <summary>
    /// Fans a single ValidationException out into one GraphQL error per broken rule.
    /// An AggregateError is expanded by the execution engine into separate entries in the
    /// response's errors array, which is what lets a client map each message to its field.
    /// </summary>
    /// <param name="error">The original error, used for its path.</param>
    /// <param name="validation">The validation failure.</param>
    /// <returns>One error per validation failure.</returns>
    private static IError ToValidationErrors(IError error, ValidationException validation)
    {
        List<IError> errors = [];

        foreach (ValidationFailure failure in validation.Errors)
        {
            errors.Add(ErrorBuilder.New()
                .SetMessage(failure.ErrorMessage)
                .SetCode(ValidationFailedCode)
                .SetPath(error.Path)
                .SetExtension("field", failure.PropertyName)
                .Build());
        }

        // AggregateError rejects an empty collection; FluentValidation never produces one, but
        // failing that way would replace a real error with an unrelated ArgumentException.
        return errors.Count == 0 ? error : new AggregateError(errors);
    }
}
