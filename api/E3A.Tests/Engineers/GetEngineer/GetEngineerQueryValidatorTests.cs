using E3A.Application.Engineers.GetEngineer;
using E3A.Application.Exceptions;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Engineers.GetEngineer;

public sealed class GetEngineerQueryValidatorTests
{
    private readonly GetEngineerQueryValidator _sut = new();

    [Fact]
    public void Validate_ShouldPass_WhenEngineerIdIsProvided()
    {
        var result = _sut.Validate(new GetEngineerQuery(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenEngineerIdIsEmpty()
    {
        var result = _sut.Validate(new GetEngineerQuery(Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.EngineerIdRequired);
    }
}
