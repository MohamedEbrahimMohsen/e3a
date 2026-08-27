using MediatR;

namespace Core.Notifications.Templates.GetNotificationTemplate;

public sealed record GetNotificationTemplateQuery(string Code) : IRequest<GetNotificationTemplateResult>;
