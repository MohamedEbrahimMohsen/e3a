using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using System.Runtime;
using System.Text.Json;

namespace Core.Azure.Clients;

public interface IStorageQueueClient
{
    Task<Response<SendReceipt>> SendMessageAsync<T>(T message, string managedIdentityClientId, string storageAccountQueueUrl, CancellationToken cancellationToken, TimeSpan? visibilityTimeout = null, TimeSpan? timeToLive = null);
}

public class StorageQueueClient(IMIClient miClient) : IStorageQueueClient
{
    public async Task<Response<SendReceipt>> SendMessageAsync<T>(T message, string managedIdentityClientId, string storageAccountQueueUrl, CancellationToken cancellationToken, TimeSpan? visibilityTimeout = null, TimeSpan? timeToLive = null)
    {
        var options = new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 };
        var queueServiceClient = new QueueClient(new Uri(storageAccountQueueUrl), miClient.GetCredential(managedIdentityClientId), options);
        var json = JsonSerializer.Serialize(message);

        var result = await queueServiceClient.SendMessageAsync(
            json,
            visibilityTimeout: visibilityTimeout,
            timeToLive: timeToLive,
            cancellationToken: cancellationToken
        );

        return result;
    }
}