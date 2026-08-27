using Core.Notifications.Firebase.Shared;
using MediatR;

namespace Core.Notifications.Firebase.MultilingualSendToTopic;

public sealed record MultilingualSendToTopicCommand(string Topic, MultilingualNotificationCommand Notification) : IRequest<string?>;
