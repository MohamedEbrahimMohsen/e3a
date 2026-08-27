using Core.Notifications.Exceptions;
using Core.Validation.Extensions;
using FluentValidation;

namespace Core.Notifications.Firebase.Shared;

public sealed class MultilingualNotificationCommandValidator : AbstractValidator<MultilingualNotificationCommand>
{
    public MultilingualNotificationCommandValidator()
    {
        RuleFor(x => x.TitleAr)
            .ValidateRequired(ErrorCodes.NotificationTitleArRequired);

        RuleFor(x => x.TitleEn)
            .ValidateRequired(ErrorCodes.NotificationTitleEnRequired);

        RuleFor(x => x.BodyAr)
            .ValidateRequired(ErrorCodes.NotificationBodyArRequired);

        RuleFor(x => x.BodyEn)
            .ValidateRequired(ErrorCodes.NotificationBodyEnRequired);
    }
}