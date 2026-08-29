using System.Text;
using Core.Azure.Clients;
using E3A.Application.Options;
using E3A.Application.Publishing.Shared;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace E3A.Tests.Publishing.Shared;

public sealed class TeamSnapshotReaderTests
{
    private readonly IStorageBlobClient _storageBlobClient = Substitute.For<IStorageBlobClient>();
    private readonly AzureOptions _azureOptions = new() { ManagedIdentityClientId = "managed-identity", StorageAccountUrl = "https://e3a.blob.core.windows.net", SnapshotsBlobContainerName = "snapshots" };
    private readonly Guid _versionId = Guid.NewGuid();

    [Fact]
    public async Task ReadAsync_ShouldReturnRelativePaths_WhenSnapshotBlobsExist()
    {
        var prefix = PublishBlobPaths.SnapshotPrefix(_versionId);
        StubBlobs([$"{prefix}agents/x.md", $"{prefix}skills/y/SKILL.md"]);

        var files = await TeamSnapshotReader.ReadAsync(_storageBlobClient, _azureOptions, _versionId, CancellationToken.None);

        files.Select(x => x.Path).Should().Equal("agents/x.md", "skills/y/SKILL.md");
    }

    [Fact]
    public async Task ReadAsync_ShouldReturnEmptyList_WhenNoSnapshotBlobsExist()
    {
        StubBlobs([]);

        var files = await TeamSnapshotReader.ReadAsync(_storageBlobClient, _azureOptions, _versionId, CancellationToken.None);

        files.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadAsync_ShouldSkipBlobs_WhenDownloadReturnsNull()
    {
        var prefix = PublishBlobPaths.SnapshotPrefix(_versionId);
        StubBlobs([$"{prefix}agents/x.md", $"{prefix}agents/missing.md"]);
        _storageBlobClient.DownloadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), $"{prefix}agents/missing.md", Arg.Any<CancellationToken>()).Returns((byte[]?)null);

        var files = await TeamSnapshotReader.ReadAsync(_storageBlobClient, _azureOptions, _versionId, CancellationToken.None);

        files.Select(x => x.Path).Should().Equal("agents/x.md");
    }

    [Fact]
    public async Task ReadAsync_ShouldNotWriteAnyBlob_WhenCalled()
    {
        var prefix = PublishBlobPaths.SnapshotPrefix(_versionId);
        StubBlobs([$"{prefix}agents/x.md"]);

        await TeamSnapshotReader.ReadAsync(_storageBlobClient, _azureOptions, _versionId, CancellationToken.None);

        await _storageBlobClient.DidNotReceive().UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _storageBlobClient.DidNotReceive().UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        await _storageBlobClient.DidNotReceive().DeleteByPrefixAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private void StubBlobs(List<string> blobNames)
    {
        _storageBlobClient.ListByPrefixAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(blobNames);
        _storageBlobClient.DownloadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Encoding.UTF8.GetBytes("content"));
    }
}
