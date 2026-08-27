using Core.Notifications.Firebase.Shared;
using MediatR;

namespace Core.Notifications.Firebase.SendToTopic;

public sealed record SendToTopicCommand(string Topic, NotificationCommand Notification) : IRequest<string?>;
