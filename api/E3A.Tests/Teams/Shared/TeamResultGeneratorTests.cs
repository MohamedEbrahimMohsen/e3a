using E3A.Application.Teams.Shared;
using E3A.Domain.Teams;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Teams.Shared;

public sealed class TeamResultGeneratorTests
{
    [Fact]
    public void Generate_ShouldMapTeamFields_WhenCalled()
    {
        var ownerUserId = Guid.NewGuid();
        var team = TeamFactory.WithMembers(ownerUserId, TeamFactory.Pin("alpha"), TeamFactory.Pin("beta"));

        var result = TeamResultGenerator.Generate(team);

        result.Id.Should().Be(team.Id);
        result.Slug.Should().Be(TeamFactory.DefaultSlug);
        result.DisplayName.Should().Be(TeamFactory.DefaultDisplayName);
        result.Description.Should().Be(team.Description);
        result.Tags.Should().Equal(team.Tags);
        result.Status.Should().Be(nameof(TeamStatus.Draft));
        result.LatestVersionId.Should().BeNull();
        result.MemberCount.Should().Be(2);
        result.CreatedAt.Should().Be(team.CreationDate);
        result.UpdatedAt.Should().Be(team.UpdationDate);
    }

    [Fact]
    public void GenerateDetail_ShouldOrderMembersBySortOrderThenEngineerId_WhenCalled()
    {
        var ownerUserId = Guid.NewGuid();
        var team = TeamFactory.WithMembers(ownerUserId, TeamFactory.Pin("alpha"), TeamFactory.Pin("beta"), TeamFactory.Pin("gamma"));
        team.Members.Reverse();

        var result = TeamResultGenerator.GenerateDetail(team);

        result.Members.Select(x => x.SortOrder).Should().Equal(0, 1, 2);
        result.Members.Select(x => x.EngineerSlug).Should().Equal("alpha", "beta", "gamma");
    }
}
