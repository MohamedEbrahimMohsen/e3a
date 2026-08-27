using Core.Errors;
using Core.Notifications.Exceptions;
using Core.Notifications.Repositories;
using MediatR;

namespace Core.Notifications.Templates.UpdateNotificationTemplate;

public sealed class UpdateNotificationTemplateHandler(INotificationTemplateRepository notificationTemplateRepository) : IRequestHandler<UpdateNotificationTemplateCommand>
{
    public async Task Handle(UpdateNotificationTemplateCommand request, CancellationToken cancellationToken)
    {
        var notificationTemplate = await notificationTemplateRepository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (notificationTemplate == null)
        {
            throw new NotFoundCoreException(ErrorCodes.NotificationTemplateNotFound);
        }

        notificationTemplate.Update(request.Title, request.Content, request.DeepLink, request.ImageUrl);
        await notificationTemplateRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
