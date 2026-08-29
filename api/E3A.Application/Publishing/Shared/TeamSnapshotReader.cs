using Core.Azure.Clients;
using E3A.Application.Options;

namespace E3A.Application.Publishing.Shared;

public static class TeamSnapshotReader
{
    public static async Task<List<PluginFile>> ReadAsync(IStorageBlobClient storageBlobClient, AzureOptions azureOptions, Guid versionId, CancellationToken cancellationToken)
    {
        var prefix = PublishBlobPaths.SnapshotPrefix(versionId);
        var blobNames = await storageBlobClient.ListByPrefixAsync(azureOptions.ManagedIdentityClientId, azureOptions.StorageAccountUrl, azureOptions.SnapshotsBlobContainerName, prefix, cancellationToken).ConfigureAwait(false);
        List<PluginFile> files = [];

        foreach (var blobName in blobNames)
        {
            var content = await storageBlobClient.DownloadAsync(azureOptions.ManagedIdentityClientId, azureOptions.StorageAccountUrl, azureOptions.SnapshotsBlobContainerName, blobName, cancellationToken).ConfigureAwait(false);

            if (content == null)
            {
                continue;
            }

            files.Add(new PluginFile(blobName[prefix.Length..], content));
        }

        return [.. files.OrderBy(x => x.Path, StringComparer.Ordinal)];
    }
}
