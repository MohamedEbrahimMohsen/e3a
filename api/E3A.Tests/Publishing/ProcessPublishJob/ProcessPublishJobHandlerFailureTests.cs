using System.Text;
using System.Text.Json;
using Core.Azure.Clients;
using E3A.Application.Engineers.Shared;
using E3A.Application.Exceptions;
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
using Xunit;

namespace E3A.Tests.Publishing.ProcessPublishJob;

public sealed class ProcessPublishJobHandlerFailureTests
{
    private readonly IItemVersionRepository _itemVersionRepository = Substitute.For<IItemVersionRepository>();
    private readonly IEngineerRepository _engineerRepository = Substitute.For<IEngineerRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IStorageBlobClient _storageBlobClient = Substitute.For<IStorageBlobClient>();
    private readonly AzureOptions _azureOptions = new() { ManagedIdentityClientId = "managed-identity", StorageAccountUrl = "https://e3a.blob.core.windows.net", DraftsBlobContainerName = "drafts", SnapshotsBlobContainerName = "snapshots", PublicBlobContainerName = "public" };
    private readonly Guid _ownerUserId = Guid.NewGuid();
    private readonly Engineer _engineer;
    private readonly ProcessPublishJobHandler _sut;

    public ProcessPublishJobHandlerFailureTests()
    {
        _engineer = EngineerFactory.Draft(_ownerUserId);
        _engineerRepository.GetByIdAsync(_engineer.Id, Arg.Any<CancellationToken>()).Returns(_engineer);
        _storageBlobClient.ListByPrefixAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns([]);
        _sut = new ProcessPublishJobHandler(_itemVersionRepository, _engineerRepository, Substitute.For<ITeamRepository>(), _userRepository, _storageBlobClient, Options.Create(_azureOptions), Options.Create(PublishingOptionsFactory.Default()));
    }

    [Fact]
    public async Task Handle_ShouldFailVersion_WhenEngineerIsMissing()
    {
        var version = GivenVersion(PluginFileFactory.Manifest("agents/reviewer.md"));
        _engineerRepository.GetByIdAsync(_engineer.Id, Arg.Any<CancellationToken>()).Returns((Engineer?)null);

        await _sut.Handle(new ProcessPublishJobCommand(version.Id), CancellationToken.None);

        version.Status.Should().Be(ItemVersionStatus.Failed);
        version.FailureReason.Should().Be(ErrorCodes.EngineerNotFound);
        await _itemVersionRepository.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldFailVersion_WhenSnapshotIsEmpty()
    {
        var version = GivenVersion(PluginFileFactory.Manifest("agents/reviewer.md"));

        await _sut.Handle(new ProcessPublishJobCommand(version.Id), CancellationToken.None);

        version.FailureReason.Should().Be(ErrorCodes.EngineerSnapshotEmpty);
        await _storageBlobClient.DidNotReceive().UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        await _itemVersionRepository.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldFailVersion_WhenStructureValidationFails()
    {
        var version = GivenVersion(PluginFileFactory.Manifest("docs/readme.md"));
        GivenDraftBlob("docs/readme.md");

        await _sut.Handle(new ProcessPublishJobCommand(version.Id), CancellationToken.None);

        version.Status.Should().Be(ItemVersionStatus.Failed);
        version.FailureReason.Should().Contain(ErrorCodes.PluginNoInstallableContent);
        await _storageBlobClient.DidNotReceive().UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        _engineer.Status.Should().Be(EngineerStatus.Draft);
    }

    private ItemVersion GivenVersion(ImportManifestResult manifest)
    {
        var version = ItemVersionFactory.Queued(_engineer.Id, frozenManifestJson: JsonSerializer.Serialize(manifest));
        _itemVersionRepository.GetByIdAsync(version.Id, Arg.Any<CancellationToken>()).Returns(version);

        return version;
    }

    private void GivenDraftBlob(string relativePath)
    {
        var draftPrefix = PublishBlobPaths.DraftPrefix(_ownerUserId, _engineer.Id);
        _storageBlobClient.ListByPrefixAsync(Arg.Any<string>(), Arg.Any<string>(), _azureOptions.DraftsBlobContainerName, draftPrefix, Arg.Any<CancellationToken>()).Returns([draftPrefix + relativePath]);
        _storageBlobClient.DownloadAsync(Arg.Any<string>(), Arg.Any<string>(), _azureOptions.DraftsBlobContainerName, draftPrefix + relativePath, Arg.Any<CancellationToken>()).Returns(Encoding.UTF8.GetBytes("content"));
    }
}
