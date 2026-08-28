using E3A.Application.Engineers.PublishEngineer;
using E3A.Application.Exceptions;
using E3A.Domain.Publishing;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Engineers.PublishEngineer;

public sealed class PublishEngineerValidatorTests
{
    private readonly PublishEngineerValidator _sut = new();

    [Fact]
    public void Validate_ShouldPass_WhenCommandIsValid()
    {
        _sut.Validate(new PublishEngineerCommand(Guid.NewGuid(), VersionIncrement.Patch)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenEngineerIdIsEmpty()
    {
        var result = _sut.Validate(new PublishEngineerCommand(Guid.Empty, VersionIncrement.Patch));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.EngineerIdRequired);
    }

    [Fact]
    public void Validate_ShouldFail_WhenIncrementIsNotDefined()
    {
        var result = _sut.Validate(new PublishEngineerCommand(Guid.NewGuid(), (VersionIncrement)99));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.PublishIncrementInvalid);
    }
}
