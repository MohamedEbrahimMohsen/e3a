using Core.DDD.Models;

namespace Core.Notifications.Templates.GetNotificationTemplate;

public sealed record GetNotificationTemplateResult(Guid Id, string Code, LocalizedText Title, LocalizedText Content, string? DeepLink, string? ImageUrl, bool IsSystemReserved);
