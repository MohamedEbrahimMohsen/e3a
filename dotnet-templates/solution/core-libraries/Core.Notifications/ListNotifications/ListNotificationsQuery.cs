using MediatR;

namespace Core.Notifications.ListNotifications;

public sealed record ListNotificationsQuery(int PageNumber, int PageSize) : IRequest<ListNotificationsResult>
{
    public int PageNumber { get; init; } = PageNumber <= 0 || PageNumber > 100 ? 1 : PageNumber;
    public int PageSize { get; init; } = PageSize <= 0 || PageSize >= 50 ? 10 : PageSize;
}