using E3A.Application.Teams.SetTeamMembers;
using E3A.Application.Teams.Shared;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Teams.Shared;

public sealed class TeamMemberPinResolverTests
{
    [Fact]
    public void ResolvePins_ShouldPreserveSubmittedOrder_WhenMembersAreResolved()
    {
        var first = TeamFactory.PublishedMember("alpha");
        var second = TeamFactory.PublishedMember("beta");
        var third = TeamFactory.PublishedMember("gamma");
        List<TeamMemberSelection> selections = [new(third.Engineer.Id, null), new(first.Engineer.Id, null), new(second.Engineer.Id, null)];

        var pins = TeamMemberPinResolver.ResolvePins(selections, [first.Engineer, second.Engineer, third.Engineer], [first.Version, second.Version, third.Version], []);

        pins.Select(x => x.EngineerSlug).Should().Equal("gamma", "alpha", "beta");
        pins.Select(x => x.EngineerId).Should().Equal(third.Engineer.Id, first.Engineer.Id, second.Engineer.Id);
    }
}
