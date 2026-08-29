using E3A.Application.Exceptions;
using E3A.Application.Teams.DeleteTeam;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Teams.DeleteTeam;

public sealed class DeleteTeamValidatorTests
{
    private readonly DeleteTeamValidator _sut = new();

    [Fact]
    public void Validate_ShouldPass_WhenCommandIsValid()
        => _sut.Validate(new DeleteTeamCommand(Guid.NewGuid())).IsValid.Should().BeTrue();

    [Fact]
    public void Validate_ShouldFail_WhenTeamIdIsEmpty()
    {
        var result = _sut.Validate(new DeleteTeamCommand(Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.TeamIdRequired);
    }
}
