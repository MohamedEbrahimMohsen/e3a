using Core.Azure.Clients;
using E3A.Application.Options;
using E3A.Domain.Publishing;
using MediatR;
using Microsoft.Extensions.Options;

namespace E3A.Application.Publishing.PublishRequested;

public sealed class PublishRequestedEventHandler(IStorageQueueClient storageQueueClient, IOptions<AzureOptions> azureOptions, IOptions<PublishingOptions> publishingOptions) : INotificationHandler<PublishRequestedDomainEvent>
{
    public async Task Handle(PublishRequestedDomainEvent notification, CancellationToken cancellationToken)
    {
        var azure = azureOptions.Value;
        var publishing = publishingOptions.Value;

        await storageQueueClient.SendMessageAsync(notification, azure.ManagedIdentityClientId, azure.StorageAccountQueueUrl, cancellationToken, visibilityTimeout: TimeSpan.FromSeconds(publishing.QueueVisibilityTimeoutSeconds)).ConfigureAwait(false);
    }
}
