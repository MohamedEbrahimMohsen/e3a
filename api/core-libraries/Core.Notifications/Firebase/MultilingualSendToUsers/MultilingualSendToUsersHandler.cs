using Core.DDD.Models;
using Core.Notifications.Entities;
using Core.Notifications.Repositories;
using Core.Notifications.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Core.Notifications.Firebase.MultilingualSendToUsers;

public sealed class MultilingualSendToUsersHandler(IFirebaseNotificationService firebaseNotificationService, INotificationRepository notificationRepository, IUserDeviceRepository userDeviceRepository, ILogger<MultilingualSendToUsersHandler> logger) : IRequestHandler<MultilingualSendToUsersCommand, string?>
{
    public async Task<string?> Handle(MultilingualSendToUsersCommand request, CancellationToken cancellationToken)
    {
        var tokens = await userDeviceRepository.GetTokensByUserIdsAsync(request.UserIds, cancellationToken).ConfigureAwait(false);
        if (tokens == null || tokens.Count == 0)
        {
            logger.LogWarning("Failed to send notification. Users with IDs {UserIds} not found or have no devices.", string.Join(", ", request.UserIds));
            return null;
        }

        var results = await firebaseNotificationService.MultilingualSendToDevicesAsync(tokens, request.Notification.ToData(), cancellationToken).ConfigureAwait(false);

        var notifications = new List<Notification>();
        foreach (var userId in request.UserIds)
        {
            var notification = new Notification(
                userId: userId,
                topic: null,
                title: new LocalizedText(request.Notification.TitleAr, request.Notification.TitleEn),
                body: new LocalizedText(request.Notification.BodyAr, request.Notification.BodyEn),
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
