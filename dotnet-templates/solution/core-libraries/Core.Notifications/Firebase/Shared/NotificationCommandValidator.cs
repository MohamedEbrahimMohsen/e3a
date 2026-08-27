using Core.Notifications.Exceptions;
using Core.Validation.Extensions;
using FluentValidation;

namespace Core.Notifications.Firebase.Shared;

public sealed class NotificationCommandValidator : AbstractValidator<NotificationCommand>
{
    public NotificationCommandValidator()
    {
        RuleFor(x => x.Title)
            .ValidateRequired(ErrorCodes.NotificationTitleRequired);

        RuleFor(x => x.Body)
            .ValidateRequired(ErrorCodes.NotificationBodyRequired);
    }
}
