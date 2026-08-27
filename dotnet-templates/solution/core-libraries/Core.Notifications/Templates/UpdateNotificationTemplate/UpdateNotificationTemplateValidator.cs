using Core.Notifications.Exceptions;
using Core.Validation.Extensions;
using FluentValidation;

namespace Core.Notifications.Templates.UpdateNotificationTemplate;

public sealed class UpdateNotificationTemplateValidator : AbstractValidator<UpdateNotificationTemplateCommand>
{
    public UpdateNotificationTemplateValidator()
    {
        RuleFor(x => x.Id)
            .ValidateRequired(ErrorCodes.NotificationTemplateIdRequired);

        RuleFor(x => x.Title)
            .NotNull().WithErrorCode(ErrorCodes.NotificationTemplateTitleRequired);

        RuleFor(x => x.Content)
            .NotNull().WithErrorCode(ErrorCodes.NotificationTemplateContentRequired);

        RuleFor(x => x.Title.Arabic)
            .ValidateRequired(ErrorCodes.NotificationTemplateTitleArabicRequired);

        RuleFor(x => x.Title.English)
            .ValidateRequired(ErrorCodes.NotificationTemplateTitleEnglishRequired);

        RuleFor(x => x.Content.Arabic)
            .ValidateRequired(ErrorCodes.NotificationTemplateContentArabicRequired);

        RuleFor(x => x.Content.English)
            .ValidateRequired(ErrorCodes.NotificationTemplateContentEnglishRequired);
    }
}
