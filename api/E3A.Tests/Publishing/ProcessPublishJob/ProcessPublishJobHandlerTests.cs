using System.Text;
using System.Text.Json;
using Core.Azure.Clients;
using E3A.Application.Options;
using E3A.Application.Publishing.ProcessPublishJob;
using E3A.Application.Publishing.Shared;
using E3A.Domain.Engineers;
using E3A.Domain.Identity;
using E3A.Domain.Publishing;
using E3A.Tests.Engineers.Shared;
using E3A.Tests.Publishing.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace E3A.Tests.Publishing.ProcessPublishJob;

public sealed class ProcessPublishJobHandlerTests
{
    private const string ZipBlobPath = "z/e3a-dive-backend-engineer/1.0.0.zip";
    private const string PinnedMarketplacePath = "m/e3a-dive-backend-engineer/1.0.0/marketplace.json";
    private readonly IItemVersionRepository _itemVersionRepository = Substitute.For<IItemVersionRepository>();
    private readonly IEngineerRepository _engineerRepository = Substitute.For<IEngineerRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IStorageBlobClient _storageBlobClient = Substitute.For<IStorageBlobClient>();
    private readonly AzureOptions _azureOptions = new() { ManagedIdentityClientId = "managed-identity", StorageAccountUrl = "https://e3a.blob.core.windows.net", DraftsBlobContainerName = "drafts", SnapshotsBlobContainerName = "snapshots", PublicBlobContainerName = "public" };
    private readonly PublishingOptions _publishingOptions = PublishingOptionsFactory.Default();
    private readonly Guid _ownerUserId = Guid.NewGuid();
    private readonly string _frozenManifestJson = JsonSerializer.Serialize(PluginFileFactory.Manifest("agents/reviewer.md"));
    private readonly Engineer _engineer;
    private readonly ItemVersion _version;
    private readonly ProcessPublishJobHandler _sut;

    public ProcessPublishJobHandlerTests()
    {
        _engineer = EngineerFactory.Draft(_ownerUserId);
        _version = ItemVersionFactory.Queued(_engineer.Id, frozenManifestJson: _frozenManifestJson);
        var draftPrefix = PublishBlobPaths.DraftPrefix(_ownerUserId, _engineer.Id);

        _itemVersionRepository.GetByIdAsync(_version.Id, Arg.Any<CancellationToken>()).Returns(_version);
        _engineerRepository.GetByIdAsync(_engineer.Id, Arg.Any<CancellationToken>()).Returns(_engineer);
        _storageBlobClient.ListByPrefixAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns([]);
        _storageBlobClient.ListByPrefixAsync(Arg.Any<string>(), Arg.Any<string>(), _azureOptions.DraftsBlobContainerName, draftPrefix, Arg.Any<CancellationToken>()).Returns([$"{draftPrefix}agents/reviewer.md"]);
        _storageBlobClient.DownloadAsync(Arg.Any<string>(), Arg.Any<string>(), _azureOptions.DraftsBlobContainerName, $"{draftPrefix}agents/reviewer.md", Arg.Any<CancellationToken>()).Returns(Encoding.UTF8.GetBytes("reviewer agent"));
        _sut = new ProcessPublishJobHandler(_itemVersionRepository, _engineerRepository, _userRepository, _storageBlobClient, Options.Create(_azureOptions), Options.Create(_publishingOptions));
    }

    [Fact]
    public async Task Handle_ShouldPublishVersionAndEngineer_WhenDraftIsValid()
    {
        await _sut.Handle(new ProcessPublishJobCommand(_version.Id), CancellationToken.None);

        _version.Status.Should().Be(ItemVersionStatus.Published);
        _version.ZipBlobPath.Should().Be(ZipBlobPath);
        _version.ZipSha256.Should().NotBeNullOrEmpty();
        _version.SizeBytes.Should().BeGreaterThan(0);
        _engineer.LatestVersionId.Should().Be(_version.Id);
        _engineer.Status.Should().Be(EngineerStatus.Published);
        await _itemVersionRepository.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldUploadZipWithImmutableCacheHeaders_WhenPublishing()
    {
        await _sut.Handle(new ProcessPublishJobCommand(_version.Id), CancellationToken.None);

        await _storageBlobClient.Received(1).UploadAsync(Arg.Any<Stream>(), _azureOptions.ManagedIdentityClientId, _azureOptions.StorageAccountUrl, _azureOptions.PublicBlobContainerName, ZipBlobPath, PublishBlobPaths.ZipContentType, _publishingOptions.ZipCacheControl, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldWritePinnedMarketplace_WhenPublishing()
    {
        await _sut.Handle(new ProcessPublishJobCommand(_version.Id), CancellationToken.None);

        await _storageBlobClient.Received(1).UploadAsync(Arg.Any<Stream>(), _azureOptions.ManagedIdentityClientId, _azureOptions.StorageAccountUrl, _azureOptions.PublicBlobContainerName, PinnedMarketplacePath, PublishBlobPaths.MarketplaceContentType, _publishingOptions.MarketplaceCacheControl, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSkipZipUpload_WhenBlobAlreadyExists()
    {
        _storageBlobClient.ListByPrefixAsync(Arg.Any<string>(), Arg.Any<string>(), _azureOptions.PublicBlobContainerName, ZipBlobPath, Arg.Any<CancellationToken>()).Returns([ZipBlobPath]);

        await _sut.Handle(new ProcessPublishJobCommand(_version.Id), CancellationToken.None);

        await _storageBlobClient.DidNotReceive().UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), ZipBlobPath, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        _version.Status.Should().Be(ItemVersionStatus.Published);
        _version.ZipSha256.Should().HaveLength(64);
    }

    [Fact]
    public async Task Handle_ShouldResumeFromBuilding_WhenVersionIsAlreadyBuilding()
    {
        var building = ItemVersionFactory.Building(_engineer.Id, frozenManifestJson: _frozenManifestJson);
        _itemVersionRepository.GetByIdAsync(building.Id, Arg.Any<CancellationToken>()).Returns(building);

        await _sut.Handle(new ProcessPublishJobCommand(building.Id), CancellationToken.None);

        building.Status.Should().Be(ItemVersionStatus.Published);
        await _itemVersionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
