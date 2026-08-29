using E3A.Application.Exceptions;
using E3A.Application.Teams.CreateTeam;
using E3A.Tests.Teams.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace E3A.Tests.Teams.CreateTeam;

public sealed class CreateTeamSlugValidatorTests
{
    private readonly CreateTeamValidator _sut = new(Options.Create(TeamFactory.CreateTeamsOptions()));

    [Fact]
    public void Validate_ShouldFail_WhenSlugIsEmpty()
    {
        var result = _sut.Validate(new CreateTeamCommand("  ", TeamFactory.DefaultDisplayName, null, []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.TeamSlugRequired);
    }

    [Fact]
    public void Validate_ShouldFail_WhenSlugIsTooShort()
    {
        var result = _sut.Validate(new CreateTeamCommand("ab", TeamFactory.DefaultDisplayName, null, []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.TeamSlugTooShort);
    }

    [Fact]
    public void Validate_ShouldFail_WhenSlugIsTooLong()
    {
        var result = _sut.Validate(new CreateTeamCommand(new string('a', 101), TeamFactory.DefaultDisplayName, null, []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.TeamSlugTooLong);
    }

    [Theory]
    [InlineData("Bad Slug")]
    [InlineData("bad--slug")]
    [InlineData("-bad")]
    [InlineData("bad-")]
    public void Validate_ShouldFail_WhenSlugHasInvalidCharacters(string slug)
    {
        var result = _sut.Validate(new CreateTeamCommand(slug, TeamFactory.DefaultDisplayName, null, []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.TeamSlugInvalid);
    }

    [Fact]
    public void Validate_ShouldFail_WhenSlugIsReserved()
    {
        var result = _sut.Validate(new CreateTeamCommand("admin", TeamFactory.DefaultDisplayName, null, []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.TeamSlugReserved);
    }
}
