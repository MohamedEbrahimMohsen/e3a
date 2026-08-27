using FluentValidation;

namespace Core.Notifications.ListNotifications;

public sealed class ListNotificationsQueryValidator : AbstractValidator<ListNotificationsQuery>
{
    public ListNotificationsQueryValidator()
    {
    }
}
