using Core.Notifications.Firebase.Shared;
using MediatR;

namespace Core.Notifications.Firebase.SendToAll;

public sealed record SendToAllCommand(NotificationCommand Notification) : IRequest<string?>;
