using Core.DDD.Models;
using Core.Notifications.Entities;
using Core.Notifications.Repositories;
using Core.Notifications.Services;
using MediatR;

namespace Core.Notifications.Firebase.MultilingualSendToAll;

public sealed class MultilingualSendToAllHandler(IFirebaseNotificationService firebaseNotificationService, INotificationRepository notificationRepository) : IRequestHandler<MultilingualSendToAllCommand, string?>
{
    public async Task<string?> Handle(MultilingualSendToAllCommand request, CancellationToken cancellationToken)
    {
        var results = await firebaseNotificationService.MultilingualSendToAllAsync(request.Notification.ToData(), cancellationToken).ConfigureAwait(false);

        var notification = new Notification(
            userId: null,
            topic: "all_users",
            title: new LocalizedText(request.Notification.TitleAr, request.Notification.TitleEn),
            body: new LocalizedText(request.Notification.BodyAr, request.Notification.BodyEn),
            imageUrl: request.Notification.ImageUrl,
            deepLink: request.Notification.DeepLink,
            data: request.Notification.Data,
            sourceType: NotificationFeedType.All);

        await notificationRepository.AddAsync(notification, cancellationToken).ConfigureAwait(false);
        await notificationRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return results;
    }
}
