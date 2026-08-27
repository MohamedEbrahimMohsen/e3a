using Core.Notifications.Exceptions;
using Core.Validation.Extensions;
using FluentValidation;

namespace Core.Notifications.MarkNotificationAsRead;

public sealed class MarkNotificationAsReadValidator : AbstractValidator<MarkNotificationAsReadCommand>
{
    public MarkNotificationAsReadValidator()
    {
        RuleFor(x => x.NotificationId)
            .ValidateRequired(ErrorCodes.NotificationIdRequired);
    }
}
