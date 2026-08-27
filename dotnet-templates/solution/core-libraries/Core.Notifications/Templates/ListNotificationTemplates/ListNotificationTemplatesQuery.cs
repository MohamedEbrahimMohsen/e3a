using MediatR;

namespace Core.Notifications.Templates.ListNotificationTemplates;

public sealed record ListNotificationTemplatesQuery() : IRequest<List<ListNotificationTemplatesItem>>;
