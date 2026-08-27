using Core.DDD.Models;
using Core.Notifications.Entities;
using Core.Notifications.Repositories;
using Core.Notifications.Services;
using MediatR;

namespace Core.Notifications.Firebase.SendToTopic;

public sealed class SendToTopicHandler(IFirebaseNotificationService firebaseNotificationService, INotificationRepository notificationRepository) : IRequestHandler<SendToTopicCommand, string?>
{
    public async Task<string?> Handle(SendToTopicCommand request, CancellationToken cancellationToken)
    {
        var results = await firebaseNotificationService.SendToTopicAsync(request.Topic, request.Notification.Title, request.Notification.Body, request.Notification.Data, cancellationToken).ConfigureAwait(false);
        
        var notification = new Notification(
            userId: null, 
            topic: request.Topic, 
            title: new LocalizedText(request.Notification.Title, request.Notification.Title), 
            body: new LocalizedText(request.Notification.Body, request.Notification.Body), 
            imageUrl: request.Notification.ImageUrl, 
            deepLink: request.Notification.DeepLink, 
            data: request.Notification.Data, 
            sourceType: NotificationFeedType.Topic);

        await notificationRepository.AddAsync(notification, cancellationToken).ConfigureAwait(false);
        await notificationRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return results;
    }
}
