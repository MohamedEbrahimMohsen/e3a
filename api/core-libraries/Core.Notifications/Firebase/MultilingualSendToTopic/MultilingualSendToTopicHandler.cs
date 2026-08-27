using Core.DDD.Models;
using Core.Notifications.Entities;
using Core.Notifications.Repositories;
using Core.Notifications.Services;
using MediatR;

namespace Core.Notifications.Firebase.MultilingualSendToTopic;

public sealed class MultilingualSendToTopicHandler(IFirebaseNotificationService firebaseNotificationService, INotificationRepository notificationRepository) : IRequestHandler<MultilingualSendToTopicCommand, string?>
{
    public async Task<string?> Handle(MultilingualSendToTopicCommand request, CancellationToken cancellationToken)
    {
        var results = await firebaseNotificationService.MultilingualSendToTopicAsync(request.Topic, request.Notification.ToData(), cancellationToken).ConfigureAwait(false);

        var notification = new Notification(
            userId: null,
            topic: request.Topic,
            title: new LocalizedText(request.Notification.TitleAr, request.Notification.TitleEn),
            body: new LocalizedText(request.Notification.BodyAr, request.Notification.BodyEn),
            imageUrl: request.Notification.ImageUrl,
            deepLink: request.Notification.DeepLink,
            data: request.Notification.Data,
            sourceType: NotificationFeedType.Topic);

        await notificationRepository.AddAsync(notification, cancellationToken).ConfigureAwait(false);
        await notificationRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return results;
    }
}
