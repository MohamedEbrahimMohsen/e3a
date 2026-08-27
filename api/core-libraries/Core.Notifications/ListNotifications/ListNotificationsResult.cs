namespace Core.Notifications.ListNotifications;

public sealed record ListNotificationsResult(List<NotificationResult> Notifications, long PageNumber, long PageSize, long TotalCount, long TotalPages, long? TotalUnread);
public sealed record NotificationResult(Guid Id, long Order, string Title, string Body, bool? IsRead, DateTimeOffset CreatedAt);