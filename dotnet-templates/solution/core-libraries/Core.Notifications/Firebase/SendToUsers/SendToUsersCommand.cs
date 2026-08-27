using Core.Notifications.Firebase.Shared;
using MediatR;

namespace Core.Notifications.Firebase.SendToUsers;

public sealed record SendToUsersCommand(List<Guid> UserIds, NotificationCommand Notification) : IRequest<string?>;
public sealed record SendToUserCommand(Guid UserId, NotificationCommand Notification) : IRequest<string?>;
