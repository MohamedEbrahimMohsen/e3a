using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Core.Azure.Clients;

public interface IStorageBlobClient
{
    Task<UploadResult> UploadAsync(Stream content, string managedIdentityClientId, string storageAccountUrl, string blobContainerName, string blobName, CancellationToken cancellationToken);

    Task DeleteByPrefixAsync(string managedIdentityClientId, string storageAccountUrl, string blobContainerName, string prefix, CancellationToken cancellationToken);
}

public class StorageBlobClient(IMIClient miClient) : IStorageBlobClient
{
    public async Task<UploadResult> UploadAsync(Stream content, string managedIdentityClientId, string storageAccountUrl, string blobContainerName, string blobName, CancellationToken cancellationToken)
    {
        var blobServiceClient = new BlobServiceClient(new Uri(storageAccountUrl), miClient.GetCredential(managedIdentityClientId));
        var blobContainerClient = blobServiceClient.GetBlobContainerClient(blobContainerName);
        await blobContainerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var blobClient = blobContainerClient.GetBlobClient(blobName);
        await blobClient.UploadAsync(content, cancellationToken: cancellationToken).ConfigureAwait(false);
        return new UploadResult(blobClient.Uri.ToString());
    }

    public async Task DeleteByPrefixAsync(string managedIdentityClientId, string storageAccountUrl, string blobContainerName, string prefix, CancellationToken cancellationToken)
    {
        var blobServiceClient = new BlobServiceClient(new Uri(storageAccountUrl), miClient.GetCredential(managedIdentityClientId));
        var blobContainerClient = blobServiceClient.GetBlobContainerClient(blobContainerName);
        await blobContainerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        await foreach (var blobItem in blobContainerClient.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, cancellationToken).ConfigureAwait(false))
        {
            await blobContainerClient.DeleteBlobIfExistsAsync(blobItem.Name, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}

public record UploadResult(string BlobUri);