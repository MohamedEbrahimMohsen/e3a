using E3A.Domain.Teams;
using E3A.Tests.Teams.Shared;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Teams;

public sealed class TeamMembershipTests
{
    [Fact]
    public void ReplaceMembers_ShouldAssignSequentialSortOrder_WhenPinsAreGiven()
    {
        var ownerUserId = Guid.NewGuid();
        var team = TeamFactory.Draft(ownerUserId);
        var pins = new List<TeamMemberPin> { TeamFactory.Pin("alpha"), TeamFactory.Pin("beta"), TeamFactory.Pin("gamma") };

        team.ReplaceMembers(pins, ownerUserId);

        team.Members.Select(x => x.SortOrder).Should().Equal(0, 1, 2);
        team.Members.Select(x => x.EngineerSlug).Should().Equal("alpha", "beta", "gamma");
    }

    [Fact]
    public void ReplaceMembers_ShouldDropPreviousMembers_WhenCalledAgain()
    {
        var ownerUserId = Guid.NewGuid();
        var firstPin = TeamFactory.Pin("alpha");
        var secondPin = TeamFactory.Pin("beta");
        var team = TeamFactory.WithMembers(ownerUserId, firstPin);

        team.ReplaceMembers([secondPin], ownerUserId);

        team.Members.Should().ContainSingle();
        team.Members.Select(x => x.EngineerId).Should().Equal(secondPin.EngineerId);
    }

    [Fact]
    public void ReplaceMembers_ShouldResequenceFromZero_WhenAMemberIsRemoved()
    {
        var ownerUserId = Guid.NewGuid();
        var firstPin = TeamFactory.Pin("alpha");
        var thirdPin = TeamFactory.Pin("gamma");
        var team = TeamFactory.WithMembers(ownerUserId, firstPin, TeamFactory.Pin("beta"), thirdPin);

        team.ReplaceMembers([firstPin, thirdPin], ownerUserId);

        team.Members.Select(x => x.SortOrder).Should().Equal(0, 1);
    }

    [Fact]
    public void ReplaceMembers_ShouldEmptyMembers_WhenPinsAreEmpty()
    {
        var ownerUserId = Guid.NewGuid();
        var team = TeamFactory.WithMembers(ownerUserId, TeamFactory.Pin("alpha"));
        var before = DateTimeOffset.UtcNow;

        team.ReplaceMembers([], ownerUserId);

        team.Members.Should().BeEmpty();
        team.UpdationDate.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void ReplaceMembers_ShouldCopyPinFields_WhenCalled()
    {
        var ownerUserId = Guid.NewGuid();
        var pin = TeamFactory.Pin("alpha", semanticVersion: "3.2.1");
        var team = TeamFactory.Draft(ownerUserId);

        team.ReplaceMembers([pin], ownerUserId);

        var member = team.Members.Should().ContainSingle().Subject;
        member.EngineerId.Should().Be(pin.EngineerId);
        member.EngineerSlug.Should().Be(pin.EngineerSlug);
        member.PinnedVersionId.Should().Be(pin.PinnedVersionId);
        member.PinnedSemanticVersion.Should().Be(pin.PinnedSemanticVersion);
        member.TeamId.Should().Be(team.Id);
    }
}
