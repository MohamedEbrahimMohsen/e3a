using E3A.Application.Exceptions;
using E3A.Application.Teams.CreateTeam;
using E3A.Tests.Teams.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace E3A.Tests.Teams.CreateTeam;

public sealed class CreateTeamValidatorTests
{
    private readonly CreateTeamValidator _sut = new(Options.Create(TeamFactory.CreateTeamsOptions()));

    [Fact]
    public void Validate_ShouldPass_WhenCommandIsValid()
    {
        var result = _sut.Validate(new CreateTeamCommand(TeamFactory.DefaultSlug, TeamFactory.DefaultDisplayName, "A product squad.", ["dotnet"]));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenDisplayNameIsEmpty()
    {
        var result = _sut.Validate(new CreateTeamCommand(TeamFactory.DefaultSlug, "  ", null, []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.TeamDisplayNameRequired);
    }

    [Fact]
    public void Validate_ShouldFail_WhenDisplayNameIsTooLong()
    {
        var result = _sut.Validate(new CreateTeamCommand(TeamFactory.DefaultSlug, new string('a', 101), null, []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.TeamDisplayNameTooLong);
    }

    [Fact]
    public void Validate_ShouldFail_WhenDisplayNameHasNoAsciiLetterOrDigit()
    {
        var result = _sut.Validate(new CreateTeamCommand(TeamFactory.DefaultSlug, "فريق", null, []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.TeamDisplayNameInvalid);
    }

    [Fact]
    public void Validate_ShouldFail_WhenDescriptionIsTooLong()
    {
        var result = _sut.Validate(new CreateTeamCommand(TeamFactory.DefaultSlug, TeamFactory.DefaultDisplayName, new string('a', 501), []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.TeamDescriptionTooLong);
    }

    [Fact]
    public void Validate_ShouldFail_WhenTagsExceedTheLimit()
    {
        var tags = Enumerable.Range(0, 11).Select(index => $"tag{index}").ToList();

        var result = _sut.Validate(new CreateTeamCommand(TeamFactory.DefaultSlug, TeamFactory.DefaultDisplayName, null, tags));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.TeamTooManyTags);
    }

    [Fact]
    public void Validate_ShouldFail_WhenATagIsEmpty()
    {
        var result = _sut.Validate(new CreateTeamCommand(TeamFactory.DefaultSlug, TeamFactory.DefaultDisplayName, null, ["dotnet", "  "]));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.TeamTagRequired);
    }

    [Fact]
    public void Validate_ShouldFail_WhenATagIsTooLong()
    {
        var result = _sut.Validate(new CreateTeamCommand(TeamFactory.DefaultSlug, TeamFactory.DefaultDisplayName, null, [new string('a', 31)]));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.TeamTagTooLong);
    }
}
