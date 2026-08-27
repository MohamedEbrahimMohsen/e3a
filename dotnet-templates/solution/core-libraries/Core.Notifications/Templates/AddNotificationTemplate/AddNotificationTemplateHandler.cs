using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Notifications.Entities;
using Core.Notifications.Exceptions;
using Core.Notifications.Repositories;
using Core.Notifications.Templates.Shared;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace Core.Notifications.Templates.AddNotificationTemplate;

public sealed class AddNotificationTemplateHandler(INotificationTemplateRepository notificationTemplateRepository, ICurrentUserService currentUserService) : IRequestHandler<AddNotificationTemplateCommand, AddNotificationTemplateResult>
{
    public async Task<AddNotificationTemplateResult> Handle(AddNotificationTemplateCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is null)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var isNotificationTemplateCodeExists = await notificationTemplateRepository.IsCodeExists(request.Code, cancellationToken).ConfigureAwait(false);

        if (isNotificationTemplateCodeExists)
        {
            throw new BadRequestCoreException(ErrorCodes.NotificationTemplateCodeAlreadyExist);
        }

        var notificationTemplate = NotificationTemplate.Create(currentUserService.UserId.Value, request.Code, request.Title, request.Content, request.DeepLink, request.ImageUrl);
        await notificationTemplateRepository.AddAsync(notificationTemplate, cancellationToken).ConfigureAwait(false);
        await notificationTemplateRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new AddNotificationTemplateResult(notificationTemplate.Id);
    }
}
