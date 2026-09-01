using FluentValidation;
using FluentValidation.Results;
using Gateway.Presentation.Errors;
using HotChocolate;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Gateway.Presentation.Tests;

/// <summary>
/// The error filter is the only thing standing between an unhandled exception and the client, so
/// what it does and does not leak is worth asserting directly.
/// </summary>
public class GraphQlErrorFilterTests
{
    [Fact]
    public void PassesThroughErrorsThatCarryNoException()
    {
        // Syntax and document-validation errors are already safe and already meaningful.
        IError error = ErrorBuilder.New().SetMessage("Unknown field 'nope'.").Build();

        IError filtered = CreateFilter("Production").OnError(error);

        Assert.Same(error, filtered);
    }

    [Fact]
    public void MasksAnUnhandledExceptionOutsideDevelopment()
    {
        IError error = ErrorBuilder.New()
            .SetMessage("Invalid column name 'Rooom' on table MetricReadings.")
            .SetException(new InvalidOperationException("connection string: Server=prod;Password=hunter2"))
            .Build();

        IError filtered = CreateFilter("Production").OnError(error);

        Assert.Equal("INTERNAL_SERVER_ERROR", filtered.Code);
        Assert.DoesNotContain("Rooom", filtered.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", filtered.Message, StringComparison.Ordinal);
        Assert.Null(filtered.Exception);
    }

    [Fact]
    public void KeepsTheDetailInDevelopment()
    {
        IError error = ErrorBuilder.New()
            .SetMessage("Invalid column name 'Rooom'.")
            .SetException(new InvalidOperationException("boom"))
            .Build();

        IError filtered = CreateFilter("Development").OnError(error);

        Assert.Contains("Rooom", filtered.Message, StringComparison.Ordinal);
        Assert.Equal("INTERNAL_SERVER_ERROR", filtered.Code);
    }

    [Fact]
    public void FansValidationFailuresOutIntoOneErrorPerBrokenRule()
    {
        ValidationException exception = new(
        [
            new ValidationFailure("Interval", "A MINUTE interval requires both 'from' and 'to'."),
            new ValidationFailure("Rooms", "At most 50 rooms may be requested at once."),
        ]);

        IError filtered = CreateFilter("Production")
            .OnError(ErrorBuilder.New().SetMessage("Unexpected Execution Error").SetException(exception).Build());

        AggregateError aggregate = Assert.IsType<AggregateError>(filtered);

        Assert.Equal(2, aggregate.Errors.Count);
        Assert.All(aggregate.Errors, error => Assert.Equal("VALIDATION_FAILED", error.Code));
        Assert.Contains(aggregate.Errors, error => Equals(error.Extensions?["field"], "Interval"));
        Assert.Contains(aggregate.Errors, error => Equals(error.Extensions?["field"], "Rooms"));
    }

    [Fact]
    public void DoesNotMaskValidationMessagesInProduction()
    {
        // A validation message describes the client's own mistake and is safe to return.
        ValidationException exception = new([new ValidationFailure("Rooms", "At most 50 rooms.")]);

        IError filtered = CreateFilter("Production")
            .OnError(ErrorBuilder.New().SetMessage("Unexpected Execution Error").SetException(exception).Build());

        AggregateError aggregate = Assert.IsType<AggregateError>(filtered);

        Assert.Equal("At most 50 rooms.", aggregate.Errors[0].Message);
    }

    private static GraphQlErrorFilter CreateFilter(string environmentName)
    {
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);

        return new GraphQlErrorFilter(NullLogger<GraphQlErrorFilter>.Instance, environment);
    }
}
