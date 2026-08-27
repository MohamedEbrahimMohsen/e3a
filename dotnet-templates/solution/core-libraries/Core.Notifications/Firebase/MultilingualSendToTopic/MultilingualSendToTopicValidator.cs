using Core.Notifications.Exceptions;
using Core.Notifications.Firebase.Shared;
using Core.Validation.Extensions;
using FluentValidation;

namespace Core.Notifications.Firebase.MultilingualSendToTopic;

public sealed class MultilingualSendToTopicValidator : AbstractValidator<MultilingualSendToTopicCommand>
{
    public MultilingualSendToTopicValidator()
    {
        RuleFor(x => x.Topic)
            .ValidateRequired(ErrorCodes.NotificationTopicRequired);

        RuleFor(x => x.Notification)
            .SetValidator(new MultilingualNotificationCommandValidator());
    }
}
