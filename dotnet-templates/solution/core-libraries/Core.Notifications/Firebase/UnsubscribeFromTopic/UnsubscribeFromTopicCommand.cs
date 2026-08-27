using MediatR;

namespace Core.Notifications.Firebase.UnsubscribeFromTopic;

public sealed record UnsubscribeFromTopicCommand(List<Guid> UserIds, string Topic) : IRequest;
