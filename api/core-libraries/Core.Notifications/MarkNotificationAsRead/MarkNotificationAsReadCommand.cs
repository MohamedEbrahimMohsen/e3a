using MediatR;

namespace Core.Notifications.MarkNotificationAsRead;

public sealed record MarkNotificationAsReadCommand(Guid NotificationId) : IRequest<bool>;
