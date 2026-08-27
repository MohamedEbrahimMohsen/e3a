using Core.DDD.Entities;
using Core.DDD.Models;

namespace Core.Notifications.Entities;

public class NotificationTemplate: AuditEntity, IAuditEntity
{
    public string Code { get; private set; }
    public LocalizedText Title { get; private set; }
    public LocalizedText Content { get; private set; }
    public string? DeepLink { get; private set; }
    public string? ImageUrl { get; private set; }
    public bool IsSystemReserved { get; init; } = false;

    private NotificationTemplate(Guid? createdBy) : base(Guid.NewGuid(), createdBy) { }

    public static NotificationTemplate Create(Guid createdBy, string code, LocalizedText title, LocalizedText content, string? deepLink, string? imageUrl = null)
    {
        return new NotificationTemplate(createdBy)
        {
            Code = code.ToLowerInvariant(),
            Title = title,
            Content = content,
            DeepLink = deepLink,
            ImageUrl = imageUrl
        };
    }

    public void Update(LocalizedText title, LocalizedText content, string? deepLink, string? imageUrl = null)
    {
        Title = title;
        Content = content;
        DeepLink = deepLink;
        ImageUrl = imageUrl;
    }
}
