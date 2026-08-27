using MediatR;

namespace Core.Notifications.Templates.DeleteNotificationTemplate;

public sealed record DeleteNotificationTemplateCommand(Guid Id) : IRequest;
