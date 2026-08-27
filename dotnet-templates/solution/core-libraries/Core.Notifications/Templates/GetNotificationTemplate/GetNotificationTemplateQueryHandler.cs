using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Notifications.Exceptions;
using Core.Notifications.Repositories;
using Core.Notifications.Templates.Shared;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace Core.Notifications.Templates.GetNotificationTemplate;

public sealed class GetNotificationTemplateQueryHandler(INotificationTemplateRepository notificationTemplateRepository) : IRequestHandler<GetNotificationTemplateQuery, GetNotificationTemplateResult>
{
    public async Task<GetNotificationTemplateResult> Handle(GetNotificationTemplateQuery request, CancellationToken cancellationToken)
    {
        var notificationTemplate = await notificationTemplateRepository.FirstOrDefaultAsync(x => x.Code.ToUpper() == request.Code.ToUpper(), cancellationToken).ConfigureAwait(false);

        if (notificationTemplate == null)
        {
            throw new NotFoundCoreException(ErrorCodes.NotificationTemplateNotFound);
        }

        return new GetNotificationTemplateResult(notificationTemplate.Id, notificationTemplate.Code, notificationTemplate.Title, notificationTemplate.Content, notificationTemplate.DeepLink, notificationTemplate.ImageUrl, notificationTemplate.IsSystemReserved);
    }
}
