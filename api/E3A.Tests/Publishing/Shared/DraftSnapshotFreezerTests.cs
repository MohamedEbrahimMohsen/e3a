using System.Text;
using Core.Azure.Clients;
using E3A.Application.Options;
using E3A.Application.Publishing.Shared;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace E3A.Tests.Publishing.Shared;

public sealed class DraftSnapshotFreezerTests
{
    private readonly IStorageBlobClient _storageBlobClient = Substitute.For<IStorageBlobClient>();
    private readonly AzureOptions _azureOptions = new() { ManagedIdentityClientId = "managed-identity", StorageAccountUrl = "https://e3a.blob.core.windows.net", DraftsBlobContainerName = "drafts", SnapshotsBlobContainerName = "snapshots" };
    private readonly Guid _ownerUserId = Guid.NewGuid();
    private readonly Guid _engineerId = Guid.NewGuid();
    private readonly Guid _versionId = Guid.NewGuid();

    [Fact]
    public async Task FreezeAsync_ShouldCopyEveryDraftBlobToSnapshotPrefix_WhenDraftExists()
    {
        var draftPrefix = PublishBlobPaths.DraftPrefix(_ownerUserId, _engineerId);
        GivenDraftBlobs($"{draftPrefix}skills/house-rules/SKILL.md", $"{draftPrefix}agents/reviewer.md");

        var result = await DraftSnapshotFreezer.FreezeAsync(_storageBlobClient, _azureOptions, _ownerUserId, _engineerId, _versionId, CancellationToken.None);

        await _storageBlobClient.Received(1).DeleteByPrefixAsync(_azureOptions.ManagedIdentityClientId, _azureOptions.StorageAccountUrl, _azureOptions.SnapshotsBlobContainerName, $"{_versionId}/", Arg.Any<CancellationToken>());
        await _storageBlobClient.Received(1).UploadAsync(Arg.Any<Stream>(), _azureOptions.ManagedIdentityClientId, _azureOptions.StorageAccountUrl, _azureOptions.SnapshotsBlobContainerName, $"{_versionId}/skills/house-rules/SKILL.md", Arg.Any<CancellationToken>());
        await _storageBlobClient.Received(1).UploadAsync(Arg.Any<Stream>(), _azureOptions.ManagedIdentityClientId, _azureOptions.StorageAccountUrl, _azureOptions.SnapshotsBlobContainerName, $"{_versionId}/agents/reviewer.md", Arg.Any<CancellationToken>());
        List<string> expectedPaths = ["agents/reviewer.md", "skills/house-rules/SKILL.md"];
        result.Select(x => x.Path).Should().Equal(expectedPaths);
    }

    [Fact]
    public async Task FreezeAsync_ShouldReturnEmpty_WhenDraftPrefixHasNoBlobs()
    {
        GivenDraftBlobs();

        var result = await DraftSnapshotFreezer.FreezeAsync(_storageBlobClient, _azureOptions, _ownerUserId, _engineerId, _versionId, CancellationToken.None);

        result.Should().BeEmpty();
        await _storageBlobClient.DidNotReceive().UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FreezeAsync_ShouldSkipBlob_WhenDownloadReturnsNull()
    {
        var draftPrefix = PublishBlobPaths.DraftPrefix(_ownerUserId, _engineerId);
        GivenDraftBlobs($"{draftPrefix}agents/reviewer.md", $"{draftPrefix}agents/missing.md");
        _storageBlobClient.DownloadAsync(Arg.Any<string>(), Arg.Any<string>(), _azureOptions.DraftsBlobContainerName, $"{draftPrefix}agents/missing.md", Arg.Any<CancellationToken>()).Returns((byte[]?)null);

        var result = await DraftSnapshotFreezer.FreezeAsync(_storageBlobClient, _azureOptions, _ownerUserId, _engineerId, _versionId, CancellationToken.None);

        List<string> expectedPaths = ["agents/reviewer.md"];
        result.Select(x => x.Path).Should().Equal(expectedPaths);
        await _storageBlobClient.DidNotReceive().UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), $"{_versionId}/agents/missing.md", Arg.Any<CancellationToken>());
    }

    private void GivenDraftBlobs(params string[] draftBlobNames)
    {
        _storageBlobClient.ListByPrefixAsync(Arg.Any<string>(), Arg.Any<string>(), _azureOptions.DraftsBlobContainerName, PublishBlobPaths.DraftPrefix(_ownerUserId, _engineerId), Arg.Any<CancellationToken>()).Returns([.. draftBlobNames]);

        foreach (var draftBlobName in draftBlobNames)
        {
            _storageBlobClient.DownloadAsync(Arg.Any<string>(), Arg.Any<string>(), _azureOptions.DraftsBlobContainerName, draftBlobName, Arg.Any<CancellationToken>()).Returns(Encoding.UTF8.GetBytes($"content of {draftBlobName}"));
        }
    }
}
