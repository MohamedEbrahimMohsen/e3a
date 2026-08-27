using Core.Notifications.Exceptions;
using Core.Validation.Extensions;
using FluentValidation;

namespace Core.Notifications.Templates.GetNotificationTemplate;

public sealed class GetNotificationTemplateQueryValidator : AbstractValidator<GetNotificationTemplateQuery>
{
    public GetNotificationTemplateQueryValidator()
    {
        RuleFor(x => x.Code)
            .ValidateRequired(ErrorCodes.NotificationTemplateCodeRequired);
    }
}
