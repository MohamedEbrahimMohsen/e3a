using Core.Notifications.Exceptions;
using Core.Validation.Extensions;
using FluentValidation;

namespace Core.Notifications.Firebase.UnsubscribeFromTopic;

public sealed class UnsubscribeFromTopicValidator : AbstractValidator<UnsubscribeFromTopicCommand>
{
    public UnsubscribeFromTopicValidator()
    {
        RuleFor(x => x.Topic)
            .ValidateRequired(ErrorCodes.NotificationTopicRequired);

        RuleFor(x => x.UserIds)
            .ValidateNotEmptyList(ErrorCodes.NotificationUserIdsRequired);
    }
}
