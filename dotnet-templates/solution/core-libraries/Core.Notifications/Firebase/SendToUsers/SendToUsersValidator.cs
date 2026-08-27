using Core.Notifications.Exceptions;
using Core.Notifications.Firebase.Shared;
using Core.Validation.Extensions;
using FluentValidation;

namespace Core.Notifications.Firebase.SendToUsers;

public sealed class SendToUsersValidator : AbstractValidator<SendToUsersCommand>
{
    public SendToUsersValidator()
    {
        RuleFor(x => x.Notification)
            .SetValidator(new NotificationCommandValidator());

        RuleFor(x => x.UserIds)
            .ValidateNotEmptyList(ErrorCodes.NotificationUserIdsRequired);
    }
}
