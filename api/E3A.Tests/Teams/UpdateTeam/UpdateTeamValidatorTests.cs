using E3A.Application.Exceptions;
using E3A.Application.Teams.UpdateTeam;
using E3A.Tests.Teams.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace E3A.Tests.Teams.UpdateTeam;

public sealed class UpdateTeamValidatorTests
{
    private readonly UpdateTeamValidator _sut = new(Options.Create(TeamFactory.CreateTeamsOptions()));

    [Fact]
    public void Validate_ShouldPass_WhenSlugIsNull()
    {
        var result = _sut.Validate(new UpdateTeamCommand(Guid.NewGuid(), null, TeamFactory.DefaultDisplayName, null, []));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenTeamIdIsEmpty()
    {
        var result = _sut.Validate(new UpdateTeamCommand(Guid.Empty, null, TeamFactory.DefaultDisplayName, null, []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.TeamIdRequired);
    }

    [Fact]
    public void Validate_ShouldFail_WhenSlugIsProvidedAndInvalid()
    {
        var result = _sut.Validate(new UpdateTeamCommand(Guid.NewGuid(), "Bad Slug", TeamFactory.DefaultDisplayName, null, []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.TeamSlugInvalid);
    }
}
