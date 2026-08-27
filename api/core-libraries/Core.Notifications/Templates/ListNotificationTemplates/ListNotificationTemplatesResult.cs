using Core.DDD.Models;

namespace Core.Notifications.Templates.ListNotificationTemplates;

public sealed record ListNotificationTemplatesItem(Guid Id, string Code, LocalizedText Title, LocalizedText Content, string? DeepLink, string? ImageUrl, bool IsSystemReserved);
