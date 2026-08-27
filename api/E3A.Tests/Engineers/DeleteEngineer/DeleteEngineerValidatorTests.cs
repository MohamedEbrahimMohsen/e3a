using E3A.Application.Engineers.DeleteEngineer;
using E3A.Application.Exceptions;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Engineers.DeleteEngineer;

public sealed class DeleteEngineerValidatorTests
{
    private readonly DeleteEngineerValidator _sut = new();

    [Fact]
    public void Validate_ShouldPass_WhenEngineerIdIsProvided()
    {
        var result = _sut.Validate(new DeleteEngineerCommand(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenEngineerIdIsEmpty()
    {
        var result = _sut.Validate(new DeleteEngineerCommand(Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.EngineerIdRequired);
    }
}
