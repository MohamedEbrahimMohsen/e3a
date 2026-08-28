using Core.Azure.Clients;
using E3A.Application.Options;
using E3A.Application.Publishing.PublishRequested;
using E3A.Domain.Publishing;
using E3A.Tests.Publishing.Shared;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace E3A.Tests.Publishing.PublishRequested;

public sealed class PublishRequestedEventHandlerTests
{
    private readonly IStorageQueueClient _storageQueueClient = Substitute.For<IStorageQueueClient>();
    private readonly AzureOptions _azureOptions = new() { ManagedIdentityClientId = "managed-identity", StorageAccountQueueUrl = "https://e3a.queue.core.windows.net/publish-jobs" };
    private readonly PublishingOptions _publishingOptions = PublishingOptionsFactory.Default(queueVisibilityTimeoutSeconds: 15);
    private readonly PublishRequestedEventHandler _sut;

    public PublishRequestedEventHandlerTests()
    {
        _sut = new PublishRequestedEventHandler(_storageQueueClient, Options.Create(_azureOptions), Options.Create(_publishingOptions));
    }

    [Fact]
    public async Task Handle_ShouldSendEventToPublishQueue_WhenRaised()
    {
        var notification = new PublishRequestedDomainEvent(Guid.NewGuid());

        await _sut.Handle(notification, CancellationToken.None);

        await _storageQueueClient.Received(1).SendMessageAsync(notification, _azureOptions.ManagedIdentityClientId, _azureOptions.StorageAccountQueueUrl, Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>(), Arg.Any<TimeSpan?>());
    }

    [Fact]
    public async Task Handle_ShouldApplyConfiguredVisibilityTimeout_WhenSending()
    {
        var notification = new PublishRequestedDomainEvent(Guid.NewGuid());

        await _sut.Handle(notification, CancellationToken.None);

        await _storageQueueClient.Received(1).SendMessageAsync(notification, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), TimeSpan.FromSeconds(_publishingOptions.QueueVisibilityTimeoutSeconds), Arg.Any<TimeSpan?>());
    }
}
