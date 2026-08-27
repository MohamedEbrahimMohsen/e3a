using Core.DDD.Models;
using Core.Notifications.Entities;
using Core.Notifications.Repositories;
using Core.Notifications.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Core.Notifications.Firebase.SendToUsers;

public sealed class SendToUsersHandler(IFirebaseNotificationService firebaseNotificationService, INotificationRepository notificationRepository, IUserDeviceRepository userDeviceRepository, ILogger<SendToUsersHandler> logger) : IRequestHandler<SendToUsersCommand, string?>
{
    public async Task<string?> Handle(SendToUsersCommand request, CancellationToken cancellationToken)
    {
        var tokens = await userDeviceRepository.GetTokensByUserIdsAsync(request.UserIds, cancellationToken).ConfigureAwait(false);
        if (tokens == null || tokens.Count == 0)
        {
            logger.LogWarning("Failed to send notification. Users with IDs {UserIds} not found or have no devices.", string.Join(", ", request.UserIds));
            return null;
        }

        var results = await firebaseNotificationService.SendToDevicesAsync(tokens, request.Notification.Title, request.Notification.Body, request.Notification.Data, cancellationToken).ConfigureAwait(false);

        var notifications = new List<Notification>();
        foreach (var userId in request.UserIds)
        {
            var notification = new Notification(
                userId: userId, 
                topic: null, 
                title: new LocalizedText(request.Notification.Title, request.Notification.Title), 
                body: new LocalizedText(request.Notification.Body, request.Notification.Body), 
                imageUrl: request.Notification.ImageUrl, 
                deepLink: request.Notification.DeepLink, 
                data: request.Notification.Data, 
                sourceType: NotificationFeedType.Direct);

            notifications.Add(notification);
        }

        await notificationRepository.AddRangeAsync(notifications, cancellationToken).ConfigureAwait(false);
        await notificationRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return results;
    }
}
