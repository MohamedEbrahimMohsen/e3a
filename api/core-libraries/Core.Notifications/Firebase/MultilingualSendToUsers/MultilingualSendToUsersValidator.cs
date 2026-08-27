using Core.Notifications.Exceptions;
using Core.Notifications.Firebase.Shared;
using Core.Validation.Extensions;
using FluentValidation;

namespace Core.Notifications.Firebase.MultilingualSendToUsers;

public sealed class MultilingualSendToUsersValidator : AbstractValidator<MultilingualSendToUsersCommand>
{
    public MultilingualSendToUsersValidator()
    {
        RuleFor(x => x.Notification)
            .SetValidator(new MultilingualNotificationCommandValidator());

        RuleFor(x => x.UserIds)
            .ValidateNotEmptyList(ErrorCodes.NotificationUserIdsRequired);
    }
}
