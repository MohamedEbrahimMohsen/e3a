using E3A.Application.Exceptions;
using E3A.Application.Teams.CheckTeamSlugAvailability;
using E3A.Tests.Teams.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace E3A.Tests.Teams.CheckTeamSlugAvailability;

public sealed class CheckTeamSlugAvailabilityQueryValidatorTests
{
    private readonly CheckTeamSlugAvailabilityQueryValidator _sut = new(Options.Create(TeamFactory.CreateTeamsOptions()));

    [Fact]
    public void Validate_ShouldPass_WhenSlugIsValid()
        => _sut.Validate(new CheckTeamSlugAvailabilityQuery(TeamFactory.DefaultSlug)).IsValid.Should().BeTrue();

    [Fact]
    public void Validate_ShouldFail_WhenSlugIsEmpty()
    {
        var result = _sut.Validate(new CheckTeamSlugAvailabilityQuery("  "));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.TeamSlugRequired);
    }

    [Fact]
    public void Validate_ShouldFail_WhenSlugIsTooShort()
    {
        var result = _sut.Validate(new CheckTeamSlugAvailabilityQuery("ab"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.TeamSlugTooShort);
    }

    [Fact]
    public void Validate_ShouldFail_WhenSlugIsTooLong()
    {
        var result = _sut.Validate(new CheckTeamSlugAvailabilityQuery(new string('a', 101)));

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
        var result = _sut.Validate(new CheckTeamSlugAvailabilityQuery(slug));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.TeamSlugInvalid);
    }

    [Fact]
    public void Validate_ShouldFail_WhenSlugIsReserved()
    {
        var result = _sut.Validate(new CheckTeamSlugAvailabilityQuery("admin"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.TeamSlugReserved);
    }
}
