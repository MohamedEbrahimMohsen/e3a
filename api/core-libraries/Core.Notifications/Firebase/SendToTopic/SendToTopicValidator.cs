using Core.Notifications.Exceptions;
using Core.Notifications.Firebase.Shared;
using Core.Validation.Extensions;
using FluentValidation;

namespace Core.Notifications.Firebase.SendToTopic;

public sealed class SendToTopicValidator : AbstractValidator<SendToTopicCommand>
{
    public SendToTopicValidator()
    {
        RuleFor(x => x.Topic)
            .ValidateRequired(ErrorCodes.NotificationTopicRequired);

        RuleFor(x => x.Notification)
            .SetValidator(new NotificationCommandValidator());
    }
}
