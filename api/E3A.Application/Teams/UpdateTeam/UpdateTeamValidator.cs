using Core.Validation.Extensions;
using E3A.Application.Exceptions;
using E3A.Application.Options;
using E3A.Domain.SharedKernel;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace E3A.Application.Teams.UpdateTeam;

public sealed class UpdateTeamValidator : AbstractValidator<UpdateTeamCommand>
{
    public UpdateTeamValidator(IOptions<TeamsOptions> teamsOptions)
    {
        var options = teamsOptions.Value;

        RuleFor(x => x.TeamId).ValidateRequired(ErrorCodes.TeamIdRequired);

        RuleFor(x => x.Slug)
            .ValidateRequired(ErrorCodes.TeamSlugRequired)
            .When(x => x.Slug != null);

        RuleFor(x => x.Slug)
            .Must(slug => SlugGenerator.NormalizeTypedSlug(slug).Length >= options.SlugMinLength)
            .WithMessage($"{{PropertyName}} must be at least {options.SlugMinLength} characters.")
            .WithErrorCode(ErrorCodes.TeamSlugTooShort)
            .When(x => x.Slug != null && !string.IsNullOrWhiteSpace(x.Slug));

        RuleFor(x => x.Slug)
            .Must(slug => SlugGenerator.NormalizeTypedSlug(slug).Length <= options.SlugMaxLength)
            .WithMessage($"{{PropertyName}} must not exceed {options.SlugMaxLength} characters.")
            .WithErrorCode(ErrorCodes.TeamSlugTooLong)
            .When(x => x.Slug != null && !string.IsNullOrWhiteSpace(x.Slug));

        RuleFor(x => x.Slug)
            .Must(slug => SlugGenerator.IsValidFormat(SlugGenerator.NormalizeTypedSlug(slug)))
            .WithMessage("{PropertyName} must be lowercase letters, digits and single hyphens.")
            .WithErrorCode(ErrorCodes.TeamSlugInvalid)
            .When(x => x.Slug != null && !string.IsNullOrWhiteSpace(x.Slug));

        RuleFor(x => x.Slug)
            .Must(slug => !options.ReservedSlugs.Contains(SlugGenerator.NormalizeTypedSlug(slug), StringComparer.OrdinalIgnoreCase))
            .WithMessage("{PropertyName} is reserved.")
            .WithErrorCode(ErrorCodes.TeamSlugReserved)
            .When(x => x.Slug != null && !string.IsNullOrWhiteSpace(x.Slug));

        RuleFor(x => x.DisplayName)
            .ValidateRequired(ErrorCodes.TeamDisplayNameRequired)
            .ValidateMaxLength(options.DisplayNameMaxLength, ErrorCodes.TeamDisplayNameTooLong);

        RuleFor(x => x.DisplayName)
            .Must(displayName => displayName.Any(char.IsAsciiLetterOrDigit))
            .WithMessage("{PropertyName} must contain at least one English letter or digit.")
            .WithErrorCode(ErrorCodes.TeamDisplayNameInvalid)
            .When(x => !string.IsNullOrWhiteSpace(x.DisplayName));

        RuleFor(x => x.Description)
            .ValidateMaxLength(options.DescriptionMaxLength, ErrorCodes.TeamDescriptionTooLong);

        RuleFor(x => x.Tags)
            .ValidateListMaxItems(options.MaxTags, ErrorCodes.TeamTooManyTags);

        RuleForEach(x => x.Tags)
            .ValidateRequired(ErrorCodes.TeamTagRequired)
            .ValidateMaxLength(options.TagMaxLength, ErrorCodes.TeamTagTooLong);
    }
}
