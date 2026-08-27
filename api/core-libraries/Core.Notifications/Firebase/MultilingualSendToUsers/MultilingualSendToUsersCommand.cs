using Core.Notifications.Firebase.Shared;
using MediatR;

namespace Core.Notifications.Firebase.MultilingualSendToUsers;

public sealed record MultilingualSendToUsersCommand(List<Guid> UserIds, MultilingualNotificationCommand Notification) : IRequest<string?>;
public sealed record MultilingualSendToUserCommand(Guid UserId, MultilingualNotificationCommand Notification) : IRequest<string?>;
