using Core.Notifications.Firebase.Shared;
using MediatR;

namespace Core.Notifications.Firebase.MultilingualSendToAll;

public sealed record MultilingualSendToAllCommand(MultilingualNotificationCommand Notification) : IRequest<string?>;
