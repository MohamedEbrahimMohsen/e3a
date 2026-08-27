using Core.Notifications.Firebase.SubscribeToTopic;
using Core.Notifications.Repositories;
using Core.Notifications.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Core.Notifications.Firebase.UnsubscribeFromTopic;

public sealed class UnsubscribeFromTopicHandler(IFirebaseNotificationService firebaseNotificationService, IUserDeviceRepository userDeviceRepository, ILogger<UnsubscribeFromTopicHandler> logger) : IRequestHandler<UnsubscribeFromTopicCommand>
{
    public async Task Handle(UnsubscribeFromTopicCommand request, CancellationToken cancellationToken)
    {
        var tokens = await userDeviceRepository.GetTokensByUserIdsAsync(request.UserIds, cancellationToken).ConfigureAwait(false);
        if (tokens == null || tokens.Count == 0)
        {
            logger.LogWarning("Failed to send notification. Users with IDs {UserIds} not found or have no devices.", string.Join(", ", request.UserIds));
            return;
        }

        await firebaseNotificationService.UnsubscribeFromTopicAsync(tokens, request.Topic).ConfigureAwait(false);
    }
}
