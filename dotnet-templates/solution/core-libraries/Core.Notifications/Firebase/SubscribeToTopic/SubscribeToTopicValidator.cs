using Core.Notifications.Exceptions;
using Core.Notifications.Firebase.Shared;
using Core.Validation.Extensions;
using FluentValidation;

namespace Core.Notifications.Firebase.SubscribeToTopic;

public sealed class SubscribeToTopicValidator : AbstractValidator<SubscribeToTopicCommand>
{
    public SubscribeToTopicValidator()
    {
        RuleFor(x => x.Topic)
            .ValidateRequired(ErrorCodes.NotificationTopicRequired);

        RuleFor(x => x.UserIds)
            .ValidateNotEmptyList(ErrorCodes.NotificationUserIdsRequired);
    }
}
