using Core.DDD.Models;
using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Localization;
using Core.Notifications.Entities;
using Core.Notifications.Exceptions;
using Core.Notifications.Repositories;
using MediatR;
using Microsoft.Extensions.Configuration;


namespace Core.Notifications.ListNotifications;

public sealed class ListNotificationsQueryHandler(INotificationRepository notificationRepository, ICurrentUserService currentUserService, IConfiguration configuration) : IRequestHandler<ListNotificationsQuery, ListNotificationsResult>
{
    public async Task<ListNotificationsResult> Handle(ListNotificationsQuery request, CancellationToken cancellationToken)
    {
        var _enableUnreadNotificationsCount = configuration.GetValue<bool>("CoreNotifications:EnableUnreadNotificationsCount");

        if (currentUserService.UserId is null)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        PageData<Notification> results = null!;

        if (currentUserService.CreatedAtUnixTimeSeconds.HasValue)
        {
             results = await notificationRepository.FindPaginatedAsync(request.PageNumber, request.PageSize, cancellationToken, 
                n => n.UserId == currentUserService.UserId || (n.UserId == null && n.CreatedAtUnixTimeSeconds >= currentUserService.CreatedAtUnixTimeSeconds.Value), 
                orderBy: n => n.OrderByDescending(n => n.CreatedAt));
        }
        else 
        {
            results = await notificationRepository.FindPaginatedAsync(request.PageNumber, request.PageSize, cancellationToken, n => n.UserId == currentUserService.UserId);
        }
        
        int? totalUnread = _enableUnreadNotificationsCount
            ? await notificationRepository.CountAsync(cancellationToken, n => n.UserId == currentUserService.UserId && n.IsRead.HasValue && !n.IsRead.Value).ConfigureAwait(false)
            : null;

        return new ListNotificationsResult
        (
            Notifications: results.Items.Select(n => new 
            NotificationResult(
                Id: n.Id, 
                Order: n.SequenceNo, 
                Title: n.Title.Localized(), 
                Body: n.Body.Localized(), 
                IsRead: n.IsRead, 
                CreatedAt: n.CreatedAt)
            ).ToList(),
            PageNumber: results.PageNumber,
            PageSize: results.PageSize,
            TotalCount: results.TotalItems,
            TotalPages: results.TotalPages,
            TotalUnread: totalUnread
        );
    }
}
