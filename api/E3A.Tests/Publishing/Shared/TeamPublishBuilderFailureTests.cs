using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using Core.Azure.Clients;
using E3A.Application.Exceptions;
using E3A.Application.Options;
using E3A.Application.Publishing.Shared;
using E3A.Application.Teams.Shared;
using E3A.Domain.Identity;
using E3A.Domain.Publishing;
using E3A.Domain.Teams;
using E3A.Tests.Teams.Shared;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace E3A.Tests.Publishing.Shared;

public sealed class TeamPublishBuilderFailureTests
{
    private readonly ITeamRepository _teamRepository = Substitute.For<ITeamRepository>();
    private readonly IItemVersionRepository _itemVersionRepository = Substitute.For<IItemVersionRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IStorageBlobClient _storageBlobClient = Substitute.For<IStorageBlobClient>();
    private readonly AzureOptions _azureOptions = new() { ManagedIdentityClientId = "managed-identity", StorageAccountUrl = "https://e3a.blob.core.windows.net", SnapshotsBlobContainerName = "snapshots", PublicBlobContainerName = "public" };
    private readonly Guid _ownerUserId = Guid.NewGuid();
    private readonly Guid _memberEngineerId = Guid.NewGuid();

    [Fact]
    public async Task BuildAsync_ShouldFail_WhenTeamDoesNotExist()
    {
        var version = ItemVersionFactory.QueuedTeam(Guid.NewGuid());
        _teamRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Team>, IQueryable<Team>>>(), Arg.Any<bool>()).Returns((Team?)null);

        var build = await BuildAsync(version);

        build.FailureReason.Should().Be(ErrorCodes.TeamNotFound);
        build.Files.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildAsync_ShouldFail_WhenRosterJsonIsUnreadable()
    {
        var build = await BuildAsync(GivenTeamVersion("null"));

        build.FailureReason.Should().Be(ErrorCodes.TeamRosterInvalid);
    }

    [Fact]
    public async Task BuildAsync_ShouldFail_WhenRosterIsEmpty()
    {
        var build = await BuildAsync(GivenTeamVersion(TeamSnapshotFactory.RosterJson()));

        build.FailureReason.Should().Be(ErrorCodes.TeamEmpty);
    }

    [Fact]
    public async Task BuildAsync_ShouldFail_WhenAPinnedVersionIsMissing()
    {
        var version = GivenTeamVersion(TeamSnapshotFactory.RosterJson(Member(Guid.NewGuid())));
        StubMemberVersions([]);

        var build = await BuildAsync(version);

        build.FailureReason.Should().Be(ErrorCodes.TeamMemberVersionNotPublished);
    }

    [Fact]
    public async Task BuildAsync_ShouldFail_WhenAPinnedVersionIsNotPublished()
    {
        var queued = ItemVersionFactory.Queued(_memberEngineerId, frozenManifestJson: ManifestJson());
        var version = GivenTeamVersion(TeamSnapshotFactory.RosterJson(Member(queued.Id)));
        StubMemberVersions([queued]);

        var build = await BuildAsync(version);

        build.FailureReason.Should().Be(ErrorCodes.TeamMemberVersionNotPublished);
    }

    [Fact]
    public async Task BuildAsync_ShouldFail_WhenAMemberManifestIsUnreadable()
    {
        var memberVersion = PublishedMemberVersion("null");
        var version = GivenTeamVersion(TeamSnapshotFactory.RosterJson(Member(memberVersion.Id)));
        StubMemberVersions([memberVersion]);

        var build = await BuildAsync(version);

        build.FailureReason.Should().Be(ErrorCodes.TeamMemberManifestInvalid);
    }

    [Fact]
    public async Task BuildAsync_ShouldFail_WhenAMemberSnapshotIsEmpty()
    {
        var memberVersion = PublishedMemberVersion(ManifestJson());
        var version = GivenTeamVersion(TeamSnapshotFactory.RosterJson(Member(memberVersion.Id)));
        StubMemberVersions([memberVersion]);
        StubSnapshot(memberVersion.Id, []);

        var build = await BuildAsync(version);

        build.FailureReason.Should().Be(ErrorCodes.TeamMemberSnapshotEmpty);
    }

    [Fact]
    public async Task BuildAsync_ShouldFail_WhenNoMemberContributesInstallableContent()
    {
        var memberVersion = PublishedMemberVersion(JsonSerializer.Serialize(PluginFileFactory.Manifest("hooks/hooks.json")));
        var version = GivenTeamVersion(TeamSnapshotFactory.RosterJson(Member(memberVersion.Id)));
        StubMemberVersions([memberVersion]);
        StubSnapshot(memberVersion.Id, ["hooks/hooks.json"]);

        var build = await BuildAsync(version);

        build.FailureReason.Should().Contain(ErrorCodes.PluginNoInstallableContent);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BuildAsync_ShouldNotWriteAnyBlob_WhenBuildFails(bool emptyRoster)
    {
        var version = emptyRoster
            ? GivenTeamVersion(TeamSnapshotFactory.RosterJson())
            : GivenTeamVersion(TeamSnapshotFactory.RosterJson(Member(Guid.NewGuid())));
        StubMemberVersions([]);

        var build = await BuildAsync(version);

        build.FailureReason.Should().NotBeNull();
        await _storageBlobClient.DidNotReceive().UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _storageBlobClient.DidNotReceive().UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    private static string ManifestJson() => JsonSerializer.Serialize(PluginFileFactory.Manifest("agents/reviewer.md"));

    private TeamRosterMemberResult Member(Guid pinnedVersionId) => new(_memberEngineerId, "alpha", pinnedVersionId, "1.0.0", 0);

    private ItemVersion PublishedMemberVersion(string frozenManifestJson)
    {
        var version = ItemVersionFactory.Queued(_memberEngineerId, frozenManifestJson: frozenManifestJson);
        version.MarkPublished(ItemVersionFactory.DefaultZipBlobPath, ItemVersionFactory.DefaultZipSha256, ItemVersionFactory.DefaultSizeBytes);
        StubSnapshot(version.Id, ["agents/reviewer.md"]);

        return version;
    }

    private ItemVersion GivenTeamVersion(string frozenRosterJson)
    {
        var team = TeamFactory.Draft(_ownerUserId);
        _teamRepository.GetByIdAsync(team.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Team>, IQueryable<Team>>>(), Arg.Any<bool>()).Returns(team);

        return ItemVersionFactory.QueuedTeam(team.Id, frozenManifestJson: frozenRosterJson);
    }

    private void StubMemberVersions(List<ItemVersion> versions)
        => _itemVersionRepository.FindAsync(Arg.Any<Expression<Func<ItemVersion, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<ItemVersion>, IQueryable<ItemVersion>>>(), Arg.Any<Func<IQueryable<ItemVersion>, IOrderedQueryable<ItemVersion>>>(), Arg.Any<bool>()).Returns(versions);

    private void StubSnapshot(Guid versionId, List<string> relativePaths)
    {
        var prefix = PublishBlobPaths.SnapshotPrefix(versionId);
        _storageBlobClient.ListByPrefixAsync(Arg.Any<string>(), Arg.Any<string>(), _azureOptions.SnapshotsBlobContainerName, prefix, Arg.Any<CancellationToken>()).Returns([.. relativePaths.Select(x => prefix + x)]);
        _storageBlobClient.DownloadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Encoding.UTF8.GetBytes("content"));
    }

    private Task<PublishBuild> BuildAsync(ItemVersion version)
        => TeamPublishBuilder.BuildAsync(_teamRepository, _itemVersionRepository, _userRepository, _storageBlobClient, _azureOptions, PublishingOptionsFactory.Default(), version, CancellationToken.None);
}
