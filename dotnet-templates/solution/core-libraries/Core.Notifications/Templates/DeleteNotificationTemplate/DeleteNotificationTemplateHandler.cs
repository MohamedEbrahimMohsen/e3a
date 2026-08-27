using Core.Errors;
using Core.Notifications.Exceptions;
using Core.Notifications.Repositories;
using MediatR;

namespace Core.Notifications.Templates.DeleteNotificationTemplate;

public sealed class DeleteNotificationTemplateHandler(INotificationTemplateRepository notificationTemplateRepository) : IRequestHandler<DeleteNotificationTemplateCommand>
{
    public async Task Handle(DeleteNotificationTemplateCommand request, CancellationToken cancellationToken)
    {
        var notificationTemplate = await notificationTemplateRepository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (notificationTemplate == null)
        {
            return;
        }

        if (notificationTemplate.IsSystemReserved)
        {
            throw new BadRequestCoreException(ErrorCodes.NotificationTemplateSystemReservedCannotDeleted);
        }

        notificationTemplate.SoftDelete();
        await notificationTemplateRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
