using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Notifications.Exceptions;
using Core.Notifications.Repositories;
using Core.Notifications.Templates.Shared;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace Core.Notifications.Templates.ListNotificationTemplates;

public sealed class ListNotificationTemplatesQueryHandler(INotificationTemplateRepository notificationTemplateRepository) : IRequestHandler<ListNotificationTemplatesQuery, List<ListNotificationTemplatesItem>>
{
    public async Task<List<ListNotificationTemplatesItem>> Handle(ListNotificationTemplatesQuery request, CancellationToken cancellationToken)
    {
        var notificationTemplates = await (notificationTemplateRepository.GetAllAsync(cancellationToken).ConfigureAwait(false)) ?? [];
        return notificationTemplates.Select(x => new ListNotificationTemplatesItem(x.Id, x.Code, x.Title, x.Content, x.DeepLink, x.ImageUrl, x.IsSystemReserved)).ToList();
    }
}
