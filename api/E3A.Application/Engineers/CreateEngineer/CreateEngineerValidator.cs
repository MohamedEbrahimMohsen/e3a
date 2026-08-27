using Core.Validation.Extensions;
using E3A.Application.Exceptions;
using E3A.Application.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace E3A.Application.Engineers.CreateEngineer;

public sealed class CreateEngineerValidator : AbstractValidator<CreateEngineerCommand>
{
    public CreateEngineerValidator(IOptions<EngineersOptions> engineersOptions)
    {
        var options = engineersOptions.Value;

        RuleFor(x => x.DisplayName)
            .ValidateRequired(ErrorCodes.EngineerDisplayNameRequired)
            .ValidateMaxLength(options.DisplayNameMaxLength, ErrorCodes.EngineerDisplayNameTooLong);

        RuleFor(x => x.DisplayName)
            .Must(displayName => displayName.Any(char.IsAsciiLetterOrDigit))
            .WithMessage("{PropertyName} must contain at least one English letter or digit.")
            .WithErrorCode(ErrorCodes.EngineerDisplayNameInvalid)
            .When(x => !string.IsNullOrWhiteSpace(x.DisplayName));

        RuleFor(x => x.Description)
            .ValidateMaxLength(options.DescriptionMaxLength, ErrorCodes.EngineerDescriptionTooLong);

        RuleFor(x => x.Tags)
            .ValidateListMaxItems(options.MaxTags, ErrorCodes.EngineerTooManyTags);

        RuleForEach(x => x.Tags)
            .ValidateRequired(ErrorCodes.EngineerTagRequired)
            .ValidateMaxLength(options.TagMaxLength, ErrorCodes.EngineerTagTooLong);
    }
}
