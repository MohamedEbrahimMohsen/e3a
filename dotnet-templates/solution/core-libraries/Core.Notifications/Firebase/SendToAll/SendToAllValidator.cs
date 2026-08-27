using Core.Notifications.Exceptions;
using Core.Notifications.Firebase.Shared;
using Core.Validation.Extensions;
using FluentValidation;

namespace Core.Notifications.Firebase.SendToAll;

public sealed class SendToAllValidator : AbstractValidator<SendToAllCommand>
{
    public SendToAllValidator()
    {
        RuleFor(x => x.Notification)
            .SetValidator(new NotificationCommandValidator());
    }
}
