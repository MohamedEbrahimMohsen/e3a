using Core.Notifications.Repositories;
using Core.Notifications.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Core.Notifications.Firebase.SubscribeToTopic;

public sealed class SubscribeToTopicHandler(IFirebaseNotificationService firebaseNotificationService, IUserDeviceRepository userDeviceRepository, ILogger<SubscribeToTopicHandler> logger) : IRequestHandler<SubscribeToTopicCommand>
{
    public async Task Handle(SubscribeToTopicCommand request, CancellationToken cancellationToken)
    {
        var tokens = await userDeviceRepository.GetTokensByUserIdsAsync(request.UserIds, cancellationToken).ConfigureAwait(false);
        if (tokens == null || tokens.Count == 0)
        {
            logger.LogWarning("Failed to send notification. Users with IDs {UserIds} not found or have no devices.", string.Join(", ", request.UserIds));
            return;
        }

        await firebaseNotificationService.SubscribeToTopicAsync(tokens, request.Topic).ConfigureAwait(false);
    }
}
