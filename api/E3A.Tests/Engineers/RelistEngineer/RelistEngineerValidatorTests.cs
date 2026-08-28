using E3A.Application.Engineers.RelistEngineer;
using E3A.Application.Exceptions;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Engineers.RelistEngineer;

public sealed class RelistEngineerValidatorTests
{
    private readonly RelistEngineerValidator _sut = new();

    [Fact]
    public void Validate_ShouldPass_WhenCommandIsValid()
    {
        var result = _sut.Validate(new RelistEngineerCommand(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenEngineerIdIsEmpty()
    {
        var result = _sut.Validate(new RelistEngineerCommand(Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.EngineerIdRequired);
    }
}
