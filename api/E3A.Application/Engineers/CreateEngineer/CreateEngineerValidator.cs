using Core.Validation.Extensions;
using E3A.Application.Exceptions;
using E3A.Application.Options;
using E3A.Domain.SharedKernel;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace E3A.Application.Engineers.CreateEngineer;

public sealed class CreateEngineerValidator : AbstractValidator<CreateEngineerCommand>
{
    public CreateEngineerValidator(IOptions<EngineersOptions> engineersOptions)
    {
        var options = engineersOptions.Value;

        RuleFor(x => x.Slug)
            .ValidateRequired(ErrorCodes.EngineerSlugRequired);

        RuleFor(x => x.Slug)
            .Must(slug => SlugGenerator.NormalizeTypedSlug(slug).Length >= options.SlugMinLength)
            .WithMessage($"{{PropertyName}} must be at least {options.SlugMinLength} characters.")
            .WithErrorCode(ErrorCodes.EngineerSlugTooShort)
            .When(x => !string.IsNullOrWhiteSpace(x.Slug));

        RuleFor(x => x.Slug)
            .Must(slug => SlugGenerator.NormalizeTypedSlug(slug).Length <= options.SlugMaxLength)
            .WithMessage($"{{PropertyName}} must not exceed {options.SlugMaxLength} characters.")
            .WithErrorCode(ErrorCodes.EngineerSlugTooLong)
            .When(x => !string.IsNullOrWhiteSpace(x.Slug));

        RuleFor(x => x.Slug)
            .Must(slug => SlugGenerator.IsValidFormat(SlugGenerator.NormalizeTypedSlug(slug)))
            .WithMessage("{PropertyName} must be lowercase letters, digits and single hyphens.")
            .WithErrorCode(ErrorCodes.EngineerSlugInvalid)
            .When(x => !string.IsNullOrWhiteSpace(x.Slug));

        RuleFor(x => x.Slug)
            .Must(slug => !options.ReservedSlugs.Contains(SlugGenerator.NormalizeTypedSlug(slug), StringComparer.OrdinalIgnoreCase))
            .WithMessage("{PropertyName} is reserved.")
            .WithErrorCode(ErrorCodes.EngineerSlugReserved)
            .When(x => !string.IsNullOrWhiteSpace(x.Slug));

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
