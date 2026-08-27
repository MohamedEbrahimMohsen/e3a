using Core.Notifications.Firebase.Shared;
using FluentValidation;

namespace Core.Notifications.Firebase.MultilingualSendToAll;

public sealed class MultilingualSendToAllValidator : AbstractValidator<MultilingualSendToAllCommand>
{
    public MultilingualSendToAllValidator()
    {
        RuleFor(x => x.Notification)
            .SetValidator(new MultilingualNotificationCommandValidator());
    }
}
