using E3A.Application.Exceptions;
using E3A.Application.Teams.GetTeam;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Teams.GetTeam;

public sealed class GetTeamQueryValidatorTests
{
    private readonly GetTeamQueryValidator _sut = new();

    [Fact]
    public void Validate_ShouldPass_WhenQueryIsValid()
        => _sut.Validate(new GetTeamQuery(Guid.NewGuid())).IsValid.Should().BeTrue();

    [Fact]
    public void Validate_ShouldFail_WhenTeamIdIsEmpty()
    {
        var result = _sut.Validate(new GetTeamQuery(Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.TeamIdRequired);
    }
}
