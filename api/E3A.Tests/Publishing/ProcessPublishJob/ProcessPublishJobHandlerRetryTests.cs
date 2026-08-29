using System.Text;
using System.Text.Json;
using Core.Azure.Clients;
using E3A.Application.Options;
using E3A.Application.Publishing.ProcessPublishJob;
using E3A.Application.Publishing.Shared;
using E3A.Domain.Engineers;
using E3A.Domain.Identity;
using E3A.Domain.Publishing;
using E3A.Domain.Teams;
using E3A.Tests.Engineers.Shared;
using E3A.Tests.Publishing.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace E3A.Tests.Publishing.ProcessPublishJob;

public sealed class ProcessPublishJobHandlerRetryTests
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
    private readonly ProcessPublishJobHandler _sut;

    public ProcessPublishJobHandlerRetryTests()
    {
        _engineer = EngineerFactory.Draft(_ownerUserId);
        var draftPrefix = PublishBlobPaths.DraftPrefix(_ownerUserId, _engineer.Id);

        _engineerRepository.GetByIdAsync(_engineer.Id, Arg.Any<CancellationToken>()).Returns(_engineer);
        _storageBlobClient.ListByPrefixAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns([]);
        _storageBlobClient.ListByPrefixAsync(Arg.Any<string>(), Arg.Any<string>(), _azureOptions.DraftsBlobContainerName, draftPrefix, Arg.Any<CancellationToken>()).Returns([$"{draftPrefix}agents/reviewer.md"]);
        _storageBlobClient.DownloadAsync(Arg.Any<string>(), Arg.Any<string>(), _azureOptions.DraftsBlobContainerName, $"{draftPrefix}agents/reviewer.md", Arg.Any<CancellationToken>()).Returns(Encoding.UTF8.GetBytes("reviewer agent"));
        _sut = new ProcessPublishJobHandler(_itemVersionRepository, _engineerRepository, Substitute.For<ITeamRepository>(), _userRepository, _storageBlobClient, Options.Create(_azureOptions), Options.Create(_publishingOptions));
    }

    [Fact]
    public async Task Handle_ShouldUploadZip_WhenOnlyAPrefixMatchingBlobExists()
    {
        var version = GivenVersion(ItemVersionFactory.Queued(_engineer.Id, frozenManifestJson: _frozenManifestJson));
        _storageBlobClient.ListByPrefixAsync(Arg.Any<string>(), Arg.Any<string>(), _azureOptions.PublicBlobContainerName, ZipBlobPath, Arg.Any<CancellationToken>()).Returns([$"{ZipBlobPath}.bak"]);

        await _sut.Handle(new ProcessPublishJobCommand(version.Id), CancellationToken.None);

        await _storageBlobClient.Received(1).UploadAsync(Arg.Any<Stream>(), _azureOptions.ManagedIdentityClientId, _azureOptions.StorageAccountUrl, _azureOptions.PublicBlobContainerName, ZipBlobPath, PublishBlobPaths.ZipContentType, _publishingOptions.ZipCacheControl, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldWritePinnedMarketplaceBeforeSaving_WhenPublishing()
    {
        var version = GivenVersion(ItemVersionFactory.Building(_engineer.Id, frozenManifestJson: _frozenManifestJson));

        await _sut.Handle(new ProcessPublishJobCommand(version.Id), CancellationToken.None);

        Received.InOrder(async () =>
        {
            await _storageBlobClient.UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), PinnedMarketplacePath, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
            await _itemVersionRepository.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Handle_ShouldNotPersistPublished_WhenPinnedMarketplaceUploadFails()
    {
        var version = GivenVersion(ItemVersionFactory.Queued(_engineer.Id, frozenManifestJson: _frozenManifestJson));
        _storageBlobClient.UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), PinnedMarketplacePath, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("pinned marketplace upload failed"));

        var act = async () => await _sut.Handle(new ProcessPublishJobCommand(version.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _itemVersionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private ItemVersion GivenVersion(ItemVersion version)
    {
        _itemVersionRepository.GetByIdAsync(version.Id, Arg.Any<CancellationToken>()).Returns(version);

        return version;
    }
}
