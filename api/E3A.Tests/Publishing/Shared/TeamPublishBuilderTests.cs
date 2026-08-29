using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using Core.Azure.Clients;
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

public sealed class TeamPublishBuilderTests
{
    private const string NewerMemberPath = "agents/refactorer.md";

    private readonly ITeamRepository _teamRepository = Substitute.For<ITeamRepository>();
    private readonly IItemVersionRepository _itemVersionRepository = Substitute.For<IItemVersionRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IStorageBlobClient _storageBlobClient = Substitute.For<IStorageBlobClient>();
    private readonly AzureOptions _azureOptions = new() { ManagedIdentityClientId = "managed-identity", StorageAccountUrl = "https://e3a.blob.core.windows.net", SnapshotsBlobContainerName = "snapshots", PublicBlobContainerName = "public" };
    private readonly Guid _ownerUserId = Guid.NewGuid();

    [Fact]
    public async Task BuildAsync_ShouldReturnTeamBuild_WhenRosterIsValid()
    {
        var scenario = GivenTwoMemberTeam();

        var build = await BuildAsync(scenario.Version);

        build.FailureReason.Should().BeNull();
        build.PluginName.Should().Be($"e3a-team-{TeamFactory.DefaultSlug}");
        build.Team.Should().NotBeNull();
        build.Engineer.Should().BeNull();
        build.Files.Select(x => x.Path).Should().Contain(["skills/alpha--house-rules/SKILL.md", "skills/beta--house-rules/SKILL.md"]);
    }

    [Fact]
    public async Task BuildAsync_ShouldReadOnlyThePinnedSnapshotPrefixes_WhenRosterHasTwoMembers()
    {
        var scenario = GivenTwoMemberTeam();

        await BuildAsync(scenario.Version);

        foreach (var pinnedVersionId in scenario.PinnedVersionIds)
        {
            await _storageBlobClient.Received(1).ListByPrefixAsync(Arg.Any<string>(), Arg.Any<string>(), _azureOptions.SnapshotsBlobContainerName, PublishBlobPaths.SnapshotPrefix(pinnedVersionId), Arg.Any<CancellationToken>());
        }

        await _storageBlobClient.Received(scenario.PinnedVersionIds.Count).ListByPrefixAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BuildAsync_ShouldNeverLoadTheMemberEngineer_WhenBuilding()
    {
        var scenario = GivenTwoMemberTeam();
        _userRepository.GetByIdAsync(_ownerUserId, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<User>, IQueryable<User>>>(), Arg.Any<bool>()).Returns(new User { UserName = "mmohsen" });

        var build = await TeamPublishBuilder.BuildAsync(_teamRepository, _itemVersionRepository, _userRepository, _storageBlobClient, _azureOptions, PublishingOptionsFactory.Default(), scenario.Version, CancellationToken.None);

        build.FailureReason.Should().BeNull();
        build.AuthorName.Should().Be("mmohsen");
    }

    [Fact]
    public async Task BuildAsync_ShouldFallBackToTeamSlugForAuthorName_WhenOwnerUserNameIsBlank()
    {
        var scenario = GivenTwoMemberTeam();

        var build = await BuildAsync(scenario.Version);

        build.AuthorName.Should().Be(TeamFactory.DefaultSlug);
    }

    [Fact]
    public async Task BuildAsync_ShouldProduceIdenticalZipSha256_WhenTheMemberEngineerHasPublishedANewerVersion()
    {
        var scenario = GivenTwoMemberTeam();

        var beforeNewerVersion = await BuildAsync(scenario.Version);
        var newerVersionOfAlpha = GivenNewerPublishedVersion(scenario, scenario.Members[0]);
        var afterNewerVersion = await BuildAsync(scenario.Version);

        afterNewerVersion.FailureReason.Should().BeNull();
        DeterministicZipper.Create(afterNewerVersion.Files).Sha256.Should().Be(DeterministicZipper.Create(beforeNewerVersion.Files).Sha256);
        afterNewerVersion.Files.Select(x => x.Path).Should().NotContain(NewerMemberPath);
        await _storageBlobClient.DidNotReceive().ListByPrefixAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), PublishBlobPaths.SnapshotPrefix(newerVersionOfAlpha.Id), Arg.Any<CancellationToken>());
    }

    private Task<PublishBuild> BuildAsync(ItemVersion version)
        => TeamPublishBuilder.BuildAsync(_teamRepository, _itemVersionRepository, _userRepository, _storageBlobClient, _azureOptions, PublishingOptionsFactory.Default(), version, CancellationToken.None);

    private TeamBuildScenario GivenTwoMemberTeam()
    {
        var team = TeamFactory.Draft(_ownerUserId);
        var alpha = GivenMember("alpha", 0);
        var beta = GivenMember("beta", 1);
        var rosterJson = JsonSerializer.Serialize(new TeamRosterResult([alpha.RosterMember, beta.RosterMember]));
        var version = ItemVersionFactory.QueuedTeam(team.Id, frozenManifestJson: rosterJson);

        _teamRepository.GetByIdAsync(team.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Team>, IQueryable<Team>>>(), Arg.Any<bool>()).Returns(team);
        _itemVersionRepository.FindAsync(Arg.Any<Expression<Func<ItemVersion, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<ItemVersion>, IQueryable<ItemVersion>>>(), Arg.Any<Func<IQueryable<ItemVersion>, IOrderedQueryable<ItemVersion>>>(), Arg.Any<bool>()).Returns([alpha.Version, beta.Version]);
        _storageBlobClient.DownloadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Encoding.UTF8.GetBytes("content"));

        return new TeamBuildScenario(version, [alpha.Version.Id, beta.Version.Id], [alpha, beta]);
    }

    private ItemVersion GivenNewerPublishedVersion(TeamBuildScenario scenario, MemberScenario member)
    {
        var manifestJson = JsonSerializer.Serialize(PluginFileFactory.Manifest("skills/house-rules/SKILL.md", NewerMemberPath));
        var newerVersion = ItemVersionFactory.Queued(member.RosterMember.EngineerId, versionNumber: 2, semanticVersion: "2.0.0", frozenManifestJson: manifestJson);
        newerVersion.MarkPublished(ItemVersionFactory.DefaultZipBlobPath, ItemVersionFactory.DefaultZipSha256, ItemVersionFactory.DefaultSizeBytes);
        var prefix = PublishBlobPaths.SnapshotPrefix(newerVersion.Id);

        _storageBlobClient.ListByPrefixAsync(Arg.Any<string>(), Arg.Any<string>(), _azureOptions.SnapshotsBlobContainerName, prefix, Arg.Any<CancellationToken>()).Returns([$"{prefix}skills/house-rules/SKILL.md", $"{prefix}{NewerMemberPath}"]);
        _itemVersionRepository.FindAsync(Arg.Any<Expression<Func<ItemVersion, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<ItemVersion>, IQueryable<ItemVersion>>>(), Arg.Any<Func<IQueryable<ItemVersion>, IOrderedQueryable<ItemVersion>>>(), Arg.Any<bool>()).Returns([.. scenario.Members.Select(x => x.Version), newerVersion]);

        return newerVersion;
    }

    private MemberScenario GivenMember(string slug, int sortOrder)
    {
        var engineerId = Guid.NewGuid();
        var manifestJson = JsonSerializer.Serialize(PluginFileFactory.Manifest("skills/house-rules/SKILL.md"));
        var version = ItemVersionFactory.Queued(engineerId, semanticVersion: "1.0.0", frozenManifestJson: manifestJson);
        version.MarkPublished(ItemVersionFactory.DefaultZipBlobPath, ItemVersionFactory.DefaultZipSha256, ItemVersionFactory.DefaultSizeBytes);
        var prefix = PublishBlobPaths.SnapshotPrefix(version.Id);
        _storageBlobClient.ListByPrefixAsync(Arg.Any<string>(), Arg.Any<string>(), _azureOptions.SnapshotsBlobContainerName, prefix, Arg.Any<CancellationToken>()).Returns([$"{prefix}skills/house-rules/SKILL.md"]);

        return new MemberScenario(version, new TeamRosterMemberResult(engineerId, slug, version.Id, "1.0.0", sortOrder));
    }

    private sealed record MemberScenario(ItemVersion Version, TeamRosterMemberResult RosterMember);

    private sealed record TeamBuildScenario(ItemVersion Version, List<Guid> PinnedVersionIds, List<MemberScenario> Members);
}
