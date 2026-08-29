using E3A.Tests.Teams.Shared;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Teams;

public sealed class TeamSlugTests
{
    [Fact]
    public void IsSlugMutable_ShouldBeTrue_WhenTeamHasNeverPublished()
    {
        var team = TeamFactory.Draft(Guid.NewGuid());

        team.IsSlugMutable.Should().BeTrue();
    }

    [Fact]
    public void IsSlugMutable_ShouldBeFalse_WhenTeamHasPublished()
    {
        var team = TeamFactory.Published(Guid.NewGuid());

        team.IsSlugMutable.Should().BeFalse();
    }
}
