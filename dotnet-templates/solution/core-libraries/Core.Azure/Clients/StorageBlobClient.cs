using Azure.Storage.Blobs;

namespace Core.Azure.Clients;

public interface IStorageBlobClient
{
    Task<UploadResult> UploadAsync(Stream content, string managedIdentityClientId, string storageAccountUrl, string blobContainerName, string blobName, CancellationToken cancellationToken);
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
}

public record UploadResult(string BlobUri);