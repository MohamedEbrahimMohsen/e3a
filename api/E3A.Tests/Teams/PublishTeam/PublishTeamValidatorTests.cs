using E3A.Application.Exceptions;
using E3A.Application.Teams.PublishTeam;
using E3A.Domain.Publishing;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Teams.PublishTeam;

public sealed class PublishTeamValidatorTests
{
    private readonly PublishTeamValidator _sut = new();

    [Fact]
    public void Validate_ShouldPass_WhenCommandIsValid()
        => _sut.Validate(new PublishTeamCommand(Guid.NewGuid(), VersionIncrement.Patch)).IsValid.Should().BeTrue();

    [Fact]
    public void Validate_ShouldFail_WhenTeamIdIsEmpty()
    {
        var result = _sut.Validate(new PublishTeamCommand(Guid.Empty, VersionIncrement.Patch));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.TeamIdRequired);
    }

    [Fact]
    public void Validate_ShouldFail_WhenIncrementIsNotAnEnumValue()
    {
        var result = _sut.Validate(new PublishTeamCommand(Guid.NewGuid(), (VersionIncrement)99));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.PublishIncrementInvalid);
    }
}
