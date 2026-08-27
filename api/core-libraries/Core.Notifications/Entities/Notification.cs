using Core.DDD.Entities;
using Core.DDD.Models;
using Core.Errors;
using Core.Notifications.Exceptions;

namespace Core.Notifications.Entities;

public class Notification : Entity, IEntity
{
    public Guid? UserId { get; init; }
    public string? Topic { get; init; }
    public long SequenceNo { get; init; }
    public LocalizedText Title { get; init; }
    public LocalizedText Body { get; init; }
    public string? ImageUrl { get; init; }
    public string? DeepLink { get; init; }
    public Dictionary<string, string>? Data { get; init; }
    public bool? IsRead { get; private set; } = false;
    public NotificationFeedType SourceType { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public long CreatedAtUnixTimeSeconds { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public DateTimeOffset? ReadAt { get; private set; } = null;

    public void MarkAsRead()
    {
        if (SourceType != NotificationFeedType.Direct)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.OnlyDirectNotificationCanBeMarked);
        }

        ReadAt = DateTimeOffset.UtcNow;
        IsRead = true;
    }

    private Notification(): base(Guid.NewGuid()) { }
    public Notification(Guid? userId, string? topic, LocalizedText title, LocalizedText body, string? imageUrl, string? deepLink, Dictionary<string, string>? data, NotificationFeedType sourceType) : base(Guid.NewGuid())
    {
        UserId = userId;
        Topic = topic;
        Title = title;
        Body = body;
        ImageUrl = imageUrl;
        DeepLink = deepLink;
        SourceType = sourceType;
        Data = data;
        IsRead = SourceType == NotificationFeedType.Direct ? false : null; // Only set IsRead for user-specific notifications, for broadcast notifications it will be null since they are not tracked per user until they are read.
    }

}

public enum NotificationFeedType
{
    Direct,
    Topic,
    All
}