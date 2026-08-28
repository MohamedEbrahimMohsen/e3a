using Core.Azure.Clients;
using E3A.Application.Options;

namespace E3A.Application.Publishing.Shared;

public static class DraftSnapshotFreezer
{
    public static async Task<List<PluginFile>> FreezeAsync(IStorageBlobClient storageBlobClient, AzureOptions azureOptions, Guid ownerUserId, Guid engineerId, Guid versionId, CancellationToken cancellationToken)
    {
        var draftPrefix = PublishBlobPaths.DraftPrefix(ownerUserId, engineerId);
        var snapshotPrefix = PublishBlobPaths.SnapshotPrefix(versionId);

        await storageBlobClient.DeleteByPrefixAsync(azureOptions.ManagedIdentityClientId, azureOptions.StorageAccountUrl, azureOptions.SnapshotsBlobContainerName, snapshotPrefix, cancellationToken).ConfigureAwait(false);

        var draftBlobNames = await storageBlobClient.ListByPrefixAsync(azureOptions.ManagedIdentityClientId, azureOptions.StorageAccountUrl, azureOptions.DraftsBlobContainerName, draftPrefix, cancellationToken).ConfigureAwait(false);
        List<PluginFile> snapshotAssets = [];

        foreach (var draftBlobName in draftBlobNames)
        {
            var content = await storageBlobClient.DownloadAsync(azureOptions.ManagedIdentityClientId, azureOptions.StorageAccountUrl, azureOptions.DraftsBlobContainerName, draftBlobName, cancellationToken).ConfigureAwait(false);

            if (content == null)
            {
                continue;
            }

            var relativePath = draftBlobName[draftPrefix.Length..];

            using var contentStream = new MemoryStream(content);
            await storageBlobClient.UploadAsync(contentStream, azureOptions.ManagedIdentityClientId, azureOptions.StorageAccountUrl, azureOptions.SnapshotsBlobContainerName, snapshotPrefix + relativePath, cancellationToken).ConfigureAwait(false);

            snapshotAssets.Add(new PluginFile(relativePath, content));
        }

        return [.. snapshotAssets.OrderBy(x => x.Path, StringComparer.Ordinal)];
    }
}
