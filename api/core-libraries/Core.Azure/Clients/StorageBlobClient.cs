using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Core.Azure.Clients;

public interface IStorageBlobClient
{
    Task<UploadResult> UploadAsync(Stream content, string managedIdentityClientId, string storageAccountUrl, string blobContainerName, string blobName, CancellationToken cancellationToken);

    Task DeleteByPrefixAsync(string managedIdentityClientId, string storageAccountUrl, string blobContainerName, string prefix, CancellationToken cancellationToken);

    Task<UploadResult> UploadAsync(Stream content, string managedIdentityClientId, string storageAccountUrl, string blobContainerName, string blobName, string contentType, string cacheControl, bool overwrite, CancellationToken cancellationToken);

    Task<List<string>> ListByPrefixAsync(string managedIdentityClientId, string storageAccountUrl, string blobContainerName, string prefix, CancellationToken cancellationToken);

    Task<byte[]?> DownloadAsync(string managedIdentityClientId, string storageAccountUrl, string blobContainerName, string blobName, CancellationToken cancellationToken);
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

    public async Task<UploadResult> UploadAsync(Stream content, string managedIdentityClientId, string storageAccountUrl, string blobContainerName, string blobName, string contentType, string cacheControl, bool overwrite, CancellationToken cancellationToken)
    {
        var blobServiceClient = new BlobServiceClient(new Uri(storageAccountUrl), miClient.GetCredential(managedIdentityClientId));
        var blobContainerClient = blobServiceClient.GetBlobContainerClient(blobContainerName);
        await blobContainerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var blobClient = blobContainerClient.GetBlobClient(blobName);
        var uploadOptions = new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType, CacheControl = cacheControl } };

        if (!overwrite)
        {
            uploadOptions.Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All };
        }

        await blobClient.UploadAsync(content, uploadOptions, cancellationToken).ConfigureAwait(false);
        return new UploadResult(blobClient.Uri.ToString());
    }

    public async Task<List<string>> ListByPrefixAsync(string managedIdentityClientId, string storageAccountUrl, string blobContainerName, string prefix, CancellationToken cancellationToken)
    {
        var blobServiceClient = new BlobServiceClient(new Uri(storageAccountUrl), miClient.GetCredential(managedIdentityClientId));
        var blobContainerClient = blobServiceClient.GetBlobContainerClient(blobContainerName);
        await blobContainerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        List<string> blobNames = [];

        await foreach (var blobItem in blobContainerClient.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, cancellationToken).ConfigureAwait(false))
        {
            blobNames.Add(blobItem.Name);
        }

        return blobNames;
    }

    public async Task<byte[]?> DownloadAsync(string managedIdentityClientId, string storageAccountUrl, string blobContainerName, string blobName, CancellationToken cancellationToken)
    {
        var blobServiceClient = new BlobServiceClient(new Uri(storageAccountUrl), miClient.GetCredential(managedIdentityClientId));
        var blobContainerClient = blobServiceClient.GetBlobContainerClient(blobContainerName);
        await blobContainerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var blobClient = blobContainerClient.GetBlobClient(blobName);

        if (!await blobClient.ExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var downloadResult = await blobClient.DownloadContentAsync(cancellationToken).ConfigureAwait(false);
        return downloadResult.Value.Content.ToArray();
    }
}

public record UploadResult(string BlobUri);