using FluentValidation.Results;
using Gateway.Application.Models;
using Gateway.Application.Validation;

namespace Gateway.Application.Tests;

public class ReadingsQueryValidatorTests
{
    private static readonly DateTime From = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly ReadingsQueryValidator validator = new();

    [Fact]
    public void AcceptsAnUnfilteredDefaultQuery()
    {
        ValidationResult result = validator.Validate(new ReadingsQuery());

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(ReadingsQueryValidator.MaxTake + 1)]
    public void RejectsATakeOutsideTheAllowedRange(int take)
    {
        ValidationResult result = validator.Validate(new ReadingsQuery { Take = take });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void AcceptsTakeAtTheMaximum()
    {
        ValidationResult result = validator.Validate(new ReadingsQuery { Take = ReadingsQueryValidator.MaxTake });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void RejectsANegativeSkip()
    {
        ValidationResult result = validator.Validate(new ReadingsQuery { Skip = -1 });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void RejectsAnInvertedReceivedAtWindow()
    {
        ValidationResult result = validator.Validate(new ReadingsQuery
        {
            ReceivedAtFrom = From,
            ReceivedAtTo = From.AddHours(-1),
        });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void RejectsAnInvertedIngestedAtWindow()
    {
        ValidationResult result = validator.Validate(new ReadingsQuery
        {
            IngestedAtFrom = From,
            IngestedAtTo = From.AddHours(-1),
        });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void AcceptsEqualFromAndTo()
    {
        ValidationResult result = validator.Validate(new ReadingsQuery
        {
            ReceivedAtFrom = From,
            ReceivedAtTo = From,
        });

        Assert.True(result.IsValid);
    }
}
