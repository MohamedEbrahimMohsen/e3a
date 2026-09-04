using E3A.Application.Teams.Shared;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Teams.Shared;

public sealed class TeamRosterGeneratorTests
{
    [Fact]
    public void Generate_ShouldOrderRosterBySortOrderThenEngineerId_WhenCalled()
    {
        var firstPin = TeamFactory.Pin("alpha", semanticVersion: "1.2.3");
        var team = TeamFactory.WithMembers(Guid.NewGuid(), firstPin, TeamFactory.Pin("beta"), TeamFactory.Pin("gamma"));
        team.Members.Reverse();

        var roster = TeamRosterGenerator.Generate(team);

        roster.Members.Select(x => x.SortOrder).Should().Equal(0, 1, 2);
        roster.Members.Select(x => x.EngineerSlug).Should().Equal("alpha", "beta", "gamma");
        roster.Members[0].EngineerId.Should().Be(firstPin.EngineerId);
        roster.Members[0].PinnedVersionId.Should().Be(firstPin.PinnedVersionId);
        roster.Members[0].PinnedSemanticVersion.Should().Be("1.2.3");
    }
}
