using Core.DDD.Models;
using Core.Notifications.Entities;
using Core.Notifications.Repositories;
using Core.Notifications.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Core.Notifications.Firebase.SendToAll;

public sealed class SendToAllHandler(IFirebaseNotificationService firebaseNotificationService, INotificationRepository notificationRepository) : IRequestHandler<SendToAllCommand, string?>
{
    public async Task<string?> Handle(SendToAllCommand request, CancellationToken cancellationToken)
    {
        var results = await firebaseNotificationService.SendToAllAsync(request.Notification.Title, request.Notification.Body, request.Notification.Data, cancellationToken).ConfigureAwait(false);

        var notification = new Notification(
            userId: null,
            topic: "all_users",
            title: new LocalizedText(request.Notification.Title, request.Notification.Title),
            body: new LocalizedText(request.Notification.Body, request.Notification.Body),
            imageUrl: request.Notification.ImageUrl,
            deepLink: request.Notification.DeepLink,
            data: request.Notification.Data,
            sourceType: NotificationFeedType.All);

        await notificationRepository.AddAsync(notification, cancellationToken).ConfigureAwait(false);
        await notificationRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return results;
    }
}
