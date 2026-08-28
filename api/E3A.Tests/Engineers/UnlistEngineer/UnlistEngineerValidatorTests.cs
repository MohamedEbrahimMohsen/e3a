using E3A.Application.Engineers.UnlistEngineer;
using E3A.Application.Exceptions;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Engineers.UnlistEngineer;

public sealed class UnlistEngineerValidatorTests
{
    private readonly UnlistEngineerValidator _sut = new();

    [Fact]
    public void Validate_ShouldPass_WhenCommandIsValid()
    {
        var result = _sut.Validate(new UnlistEngineerCommand(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenEngineerIdIsEmpty()
    {
        var result = _sut.Validate(new UnlistEngineerCommand(Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.EngineerIdRequired);
    }
}
