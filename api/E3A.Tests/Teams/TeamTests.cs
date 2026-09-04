using E3A.Domain.Teams;
using E3A.Tests.Teams.Shared;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Teams;

public sealed class TeamTests
{
    [Fact]
    public void Create_ShouldReturnDraftTeam_WhenCalled()
    {
        var ownerUserId = Guid.NewGuid();
        List<string> tags = ["dotnet", "team"];
        var before = DateTimeOffset.UtcNow;

        var team = Team.Create(ownerUserId, TeamFactory.DefaultSlug, TeamFactory.DefaultDisplayName, "A product squad.", tags);

        team.OwnerUserId.Should().Be(ownerUserId);
        team.Slug.Should().Be(TeamFactory.DefaultSlug);
        team.DisplayName.Should().Be(TeamFactory.DefaultDisplayName);
        team.Description.Should().Be("A product squad.");
        team.Tags.Should().Equal(tags);
        team.Status.Should().Be(TeamStatus.Draft);
        team.LatestVersionId.Should().BeNull();
        team.Members.Should().BeEmpty();
        team.IsDeleted.Should().BeFalse();
        team.Id.Should().NotBe(Guid.Empty);
        team.CreatedBy.Should().Be(ownerUserId);
        team.CreationDate.Should().BeOnOrAfter(before);
        team.UpdationDate.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void Create_ShouldCopyTags_WhenSourceListIsMutatedAfterwards()
    {
        List<string> tags = ["dotnet"];
        var team = Team.Create(Guid.NewGuid(), TeamFactory.DefaultSlug, TeamFactory.DefaultDisplayName, null, tags);

        tags.Add("team");

        team.Tags.Should().ContainSingle().Which.Should().Be("dotnet");
    }

    [Fact]
    public void UpdateMetadata_ShouldReplaceFieldsAndAdvanceUpdationDate_WhenCalled()
    {
        var team = TeamFactory.Draft(Guid.NewGuid());
        var before = DateTimeOffset.UtcNow;

        team.UpdateMetadata("Platform Squad", "A platform squad.", ["platform"]);

        team.DisplayName.Should().Be("Platform Squad");
        team.Description.Should().Be("A platform squad.");
        team.Tags.Should().Equal("platform");
        team.Slug.Should().Be(TeamFactory.DefaultSlug);
        team.UpdationDate.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void ChangeSlug_ShouldReplaceSlugAndAdvanceUpdationDate_WhenCalled()
    {
        var team = TeamFactory.Draft(Guid.NewGuid());
        var before = DateTimeOffset.UtcNow;

        team.ChangeSlug("platform-squad");

        team.Slug.Should().Be("platform-squad");
        team.UpdationDate.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void MarkPublished_ShouldSetPublishedStatusAndLatestVersion_WhenCalled()
    {
        var team = TeamFactory.Draft(Guid.NewGuid());
        var latestVersionId = Guid.NewGuid();
        var before = DateTimeOffset.UtcNow;

        team.MarkPublished(latestVersionId);

        team.Status.Should().Be(TeamStatus.Published);
        team.LatestVersionId.Should().Be(latestVersionId);
        team.UpdationDate.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void Delete_ShouldSetDeletedStatusAndSoftDelete_WhenCalled()
    {
        var team = TeamFactory.Draft(Guid.NewGuid());
        var before = DateTimeOffset.UtcNow;

        team.Delete();

        team.Status.Should().Be(TeamStatus.Deleted);
        team.IsDeleted.Should().BeTrue();
        team.UpdationDate.Should().BeOnOrAfter(before);
    }
}
