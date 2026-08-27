using MediatR;

namespace Core.Notifications.Firebase.SubscribeToTopic;

public sealed record SubscribeToTopicCommand(List<Guid> UserIds, string Topic) : IRequest;
