using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using Core.Azure.Clients;
using E3A.Application.Exceptions;
using E3A.Application.Options;
using E3A.Application.Publishing.ProcessPublishJob;
using E3A.Application.Publishing.Shared;
using E3A.Application.Teams.Shared;
using E3A.Domain.Engineers;
using E3A.Domain.Identity;
using E3A.Domain.Publishing;
using E3A.Domain.Teams;
using E3A.Tests.Publishing.Shared;
using E3A.Tests.Teams.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace E3A.Tests.Publishing.ProcessPublishJob;

public sealed class ProcessPublishJobHandlerTeamTests
{
    private const string ZipBlobPath = "z/e3a-team-dotnet-product-squad/1.0.0.zip";
    private const string PinnedMarketplacePath = "m/e3a-team-dotnet-product-squad/1.0.0/marketplace.json";

    private readonly IItemVersionRepository _itemVersionRepository = Substitute.For<IItemVersionRepository>();
    private readonly IEngineerRepository _engineerRepository = Substitute.For<IEngineerRepository>();
    private readonly ITeamRepository _teamRepository = Substitute.For<ITeamRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IStorageBlobClient _storageBlobClient = Substitute.For<IStorageBlobClient>();
    private readonly AzureOptions _azureOptions = new() { ManagedIdentityClientId = "managed-identity", StorageAccountUrl = "https://e3a.blob.core.windows.net", SnapshotsBlobContainerName = "snapshots", PublicBlobContainerName = "public" };
    private readonly PublishingOptions _publishingOptions = PublishingOptionsFactory.Default();
    private readonly Guid _ownerUserId = Guid.NewGuid();
    private readonly Guid _memberEngineerId = Guid.NewGuid();
    private readonly Team _team;
    private readonly ProcessPublishJobHandler _sut;

    public ProcessPublishJobHandlerTeamTests()
    {
        _team = TeamFactory.Draft(_ownerUserId);
        _teamRepository.GetByIdAsync(_team.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Team>, IQueryable<Team>>>(), Arg.Any<bool>()).Returns(_team);
        _storageBlobClient.ListByPrefixAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns([]);
        _storageBlobClient.DownloadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Encoding.UTF8.GetBytes("content"));
        _sut = new ProcessPublishJobHandler(_itemVersionRepository, _engineerRepository, _teamRepository, _userRepository, _storageBlobClient, Options.Create(_azureOptions), Options.Create(_publishingOptions));
    }

    [Fact]
    public async Task Handle_ShouldPublishTeamVersionAndTeam_WhenRosterIsValid()
    {
        var version = GivenValidTeamVersion();

        await _sut.Handle(new ProcessPublishJobCommand(version.Id), CancellationToken.None);

        version.Status.Should().Be(ItemVersionStatus.Published);
        version.ZipBlobPath.Should().Be(ZipBlobPath);
        version.ZipSha256.Should().HaveLength(64);
        _team.LatestVersionId.Should().Be(version.Id);
        _team.Status.Should().Be(TeamStatus.Published);
        await _itemVersionRepository.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldUploadTeamZipWithImmutableCacheHeaders_WhenPublishing()
    {
        var version = GivenValidTeamVersion();

        await _sut.Handle(new ProcessPublishJobCommand(version.Id), CancellationToken.None);

        await _storageBlobClient.Received(1).UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), _azureOptions.PublicBlobContainerName, ZipBlobPath, PublishBlobPaths.ZipContentType, _publishingOptions.ZipCacheControl, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldWriteTeamPinnedMarketplace_WhenPublishing()
    {
        var version = GivenValidTeamVersion();

        await _sut.Handle(new ProcessPublishJobCommand(version.Id), CancellationToken.None);

        await _storageBlobClient.Received(1).UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), _azureOptions.PublicBlobContainerName, PinnedMarketplacePath, PublishBlobPaths.MarketplaceContentType, _publishingOptions.MarketplaceCacheControl, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldFailVersionAndTouchNoPublicBlob_WhenTeamBuildFails()
    {
        var version = GivenTeamVersion(TeamSnapshotFactory.RosterJson());

        await _sut.Handle(new ProcessPublishJobCommand(version.Id), CancellationToken.None);

        version.Status.Should().Be(ItemVersionStatus.Failed);
        version.FailureReason.Should().Be(ErrorCodes.TeamEmpty);
        _team.Status.Should().Be(TeamStatus.Draft);
        await _storageBlobClient.DidNotReceive().UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), _azureOptions.PublicBlobContainerName, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        await _itemVersionRepository.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldNotMarkTheEngineerPublished_WhenVersionIsATeamVersion()
    {
        var version = GivenValidTeamVersion();

        await _sut.Handle(new ProcessPublishJobCommand(version.Id), CancellationToken.None);

        _engineerRepository.DidNotReceive().Update(Arg.Any<Engineer>());
    }

    [Fact]
    public async Task Handle_ShouldResumeFromBuilding_WhenTeamVersionIsAlreadyBuilding()
    {
        var version = GivenValidTeamVersion();
        version.MarkBuilding();

        await _sut.Handle(new ProcessPublishJobCommand(version.Id), CancellationToken.None);

        version.Status.Should().Be(ItemVersionStatus.Published);
        await _itemVersionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private ItemVersion GivenValidTeamVersion()
    {
        var manifestJson = JsonSerializer.Serialize(PluginFileFactory.Manifest("agents/reviewer.md"));
        var memberVersion = ItemVersionFactory.Queued(_memberEngineerId, frozenManifestJson: manifestJson);
        memberVersion.MarkPublished(ItemVersionFactory.DefaultZipBlobPath, ItemVersionFactory.DefaultZipSha256, ItemVersionFactory.DefaultSizeBytes);

        var prefix = PublishBlobPaths.SnapshotPrefix(memberVersion.Id);
        _storageBlobClient.ListByPrefixAsync(Arg.Any<string>(), Arg.Any<string>(), _azureOptions.SnapshotsBlobContainerName, prefix, Arg.Any<CancellationToken>()).Returns([$"{prefix}agents/reviewer.md"]);
        _itemVersionRepository.FindAsync(Arg.Any<Expression<Func<ItemVersion, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<ItemVersion>, IQueryable<ItemVersion>>>(), Arg.Any<Func<IQueryable<ItemVersion>, IOrderedQueryable<ItemVersion>>>(), Arg.Any<bool>()).Returns([memberVersion]);

        return GivenTeamVersion(TeamSnapshotFactory.RosterJson(new TeamRosterMemberResult(_memberEngineerId, "alpha", memberVersion.Id, "1.0.0", 0)));
    }

    private ItemVersion GivenTeamVersion(string frozenRosterJson)
    {
        var version = ItemVersionFactory.QueuedTeam(_team.Id, frozenManifestJson: frozenRosterJson);
        _itemVersionRepository.GetByIdAsync(version.Id, Arg.Any<CancellationToken>()).Returns(version);

        return version;
    }
}
