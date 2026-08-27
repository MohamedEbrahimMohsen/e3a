using E3A.Domain.Engineers;
using E3A.Tests.Engineers.Shared;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Engineers;

public sealed class EngineerTests
{
    [Fact]
    public void Create_ShouldReturnDraftEngineer_WhenDataIsProvided()
    {
        var ownerUserId = Guid.NewGuid();
        List<string> tags = ["dotnet", "ddd"];

        var engineer = Engineer.Create(ownerUserId, "dive-backend-engineer", "Dive Backend Engineer", "A backend engineer.", tags);

        engineer.OwnerUserId.Should().Be(ownerUserId);
        engineer.Slug.Should().Be("dive-backend-engineer");
        engineer.DisplayName.Should().Be("Dive Backend Engineer");
        engineer.Description.Should().Be("A backend engineer.");
        engineer.Tags.Should().Equal(tags);
        engineer.Status.Should().Be(EngineerStatus.Draft);
        engineer.InstallCount.Should().Be(0);
        engineer.LatestVersionId.Should().BeNull();
        engineer.DraftManifestJson.Should().BeNull();
        engineer.IsDeleted.Should().BeFalse();
        engineer.Id.Should().NotBe(Guid.Empty);
        engineer.CreatedBy.Should().Be(ownerUserId);
    }

    [Fact]
    public void Create_ShouldStampUtcAuditDates_WhenEngineerIsCreated()
    {
        var before = DateTimeOffset.UtcNow;

        var engineer = EngineerFactory.Draft(Guid.NewGuid());

        engineer.CreationDate.Should().BeOnOrAfter(before);
        engineer.UpdationDate.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void Create_ShouldCopyTags_WhenSourceListIsMutatedAfterwards()
    {
        List<string> tags = ["dotnet"];
        var engineer = Engineer.Create(Guid.NewGuid(), "dive-backend-engineer", "Dive Backend Engineer", null, tags);

        tags.Add("ddd");

        engineer.Tags.Should().ContainSingle().Which.Should().Be("dotnet");
    }

    [Fact]
    public void UpdateMetadata_ShouldReplaceMetadata_WhenCalled()
    {
        var ownerUserId = Guid.NewGuid();
        var engineer = EngineerFactory.Draft(ownerUserId);
        var before = DateTimeOffset.UtcNow;

        engineer.UpdateMetadata("Dive Frontend Engineer", "A frontend engineer.", ["react"]);

        engineer.DisplayName.Should().Be("Dive Frontend Engineer");
        engineer.Description.Should().Be("A frontend engineer.");
        engineer.Tags.Should().Equal("react");
        engineer.Slug.Should().Be(EngineerFactory.DefaultSlug);
        engineer.OwnerUserId.Should().Be(ownerUserId);
        engineer.UpdationDate.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void UpdateMetadata_ShouldCopyTags_WhenSourceListIsMutatedAfterwards()
    {
        var engineer = EngineerFactory.Draft(Guid.NewGuid());
        List<string> tags = ["react"];

        engineer.UpdateMetadata("Dive Frontend Engineer", null, tags);
        tags.Add("vue");

        engineer.Tags.Should().ContainSingle().Which.Should().Be("react");
    }

    [Fact]
    public void MarkPublished_ShouldSetStatusAndLatestVersion_WhenCalled()
    {
        var engineer = EngineerFactory.Draft(Guid.NewGuid());
        var latestVersionId = Guid.NewGuid();
        var before = DateTimeOffset.UtcNow;

        engineer.MarkPublished(latestVersionId);

        engineer.Status.Should().Be(EngineerStatus.Published);
        engineer.LatestVersionId.Should().Be(latestVersionId);
        engineer.UpdationDate.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void ReplaceDraftManifest_ShouldStoreManifestJson_WhenCalled()
    {
        var engineer = EngineerFactory.Draft(Guid.NewGuid());

        engineer.ReplaceDraftManifest("""{"imported":[]}""");

        engineer.DraftManifestJson.Should().Be("""{"imported":[]}""");
    }

    [Fact]
    public void ReplaceDraftManifest_ShouldAdvanceUpdationDate_WhenCalled()
    {
        var engineer = EngineerFactory.Draft(Guid.NewGuid());
        var before = DateTimeOffset.UtcNow;

        engineer.ReplaceDraftManifest("""{"imported":[]}""");

        engineer.UpdationDate.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void Delete_ShouldMarkDeletedAndSoftDeleted_WhenCalled()
    {
        var engineer = EngineerFactory.Draft(Guid.NewGuid());
        var before = DateTimeOffset.UtcNow;

        engineer.Delete();

        engineer.Status.Should().Be(EngineerStatus.Deleted);
        engineer.IsDeleted.Should().BeTrue();
        engineer.UpdationDate.Should().BeOnOrAfter(before);
    }
}
