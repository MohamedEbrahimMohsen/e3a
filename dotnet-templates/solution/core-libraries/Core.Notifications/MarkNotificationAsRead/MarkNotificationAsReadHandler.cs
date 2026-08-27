using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Notifications.Exceptions;
using Core.Notifications.Repositories;
using MediatR;

namespace Core.Notifications.MarkNotificationAsRead;

public sealed class MarkNotificationAsReadHandler(INotificationRepository notificationRepository, ICurrentUserService currentUserService) : IRequestHandler<MarkNotificationAsReadCommand, bool>
{
    public async Task<bool> Handle(MarkNotificationAsReadCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is null)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var notification = await notificationRepository.GetByIdAsync(request.NotificationId, cancellationToken);
        
        if (notification is null)
        {
            return false;
        }

        if (notification.UserId != currentUserService.UserId)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        notification.MarkAsRead();
        await notificationRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
