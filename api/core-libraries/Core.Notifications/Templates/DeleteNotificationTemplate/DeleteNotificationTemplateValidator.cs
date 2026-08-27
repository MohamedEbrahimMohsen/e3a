using Core.Notifications.Exceptions;
using Core.Validation.Extensions;
using FluentValidation;

namespace Core.Notifications.Templates.DeleteNotificationTemplate;

public sealed class DeleteNotificationTemplateValidator : AbstractValidator<DeleteNotificationTemplateCommand>
{
    public DeleteNotificationTemplateValidator()
    {
        RuleFor(x => x.Id)
            .ValidateRequired(ErrorCodes.NotificationTemplateIdRequired);
    }
}
