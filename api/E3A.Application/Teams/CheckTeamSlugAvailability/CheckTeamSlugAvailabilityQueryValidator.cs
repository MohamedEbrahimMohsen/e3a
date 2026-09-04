using Core.Validation.Extensions;
using E3A.Application.Exceptions;
using E3A.Application.Options;
using E3A.Domain.SharedKernel;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace E3A.Application.Teams.CheckTeamSlugAvailability;

public sealed class CheckTeamSlugAvailabilityQueryValidator : AbstractValidator<CheckTeamSlugAvailabilityQuery>
{
    public CheckTeamSlugAvailabilityQueryValidator(IOptions<TeamsOptions> teamsOptions)
    {
        var options = teamsOptions.Value;

        RuleFor(x => x.Slug)
            .ValidateRequired(ErrorCodes.TeamSlugRequired);

        RuleFor(x => x.Slug)
            .Must(slug => SlugGenerator.NormalizeTypedSlug(slug).Length >= options.SlugMinLength)
            .WithMessage($"{{PropertyName}} must be at least {options.SlugMinLength} characters.")
            .WithErrorCode(ErrorCodes.TeamSlugTooShort)
            .When(x => !string.IsNullOrWhiteSpace(x.Slug));

        RuleFor(x => x.Slug)
            .Must(slug => SlugGenerator.NormalizeTypedSlug(slug).Length <= options.SlugMaxLength)
            .WithMessage($"{{PropertyName}} must not exceed {options.SlugMaxLength} characters.")
            .WithErrorCode(ErrorCodes.TeamSlugTooLong)
            .When(x => !string.IsNullOrWhiteSpace(x.Slug));

        RuleFor(x => x.Slug)
            .Must(slug => SlugGenerator.IsValidFormat(SlugGenerator.NormalizeTypedSlug(slug)))
            .WithMessage("{PropertyName} must be lowercase letters, digits and single hyphens.")
            .WithErrorCode(ErrorCodes.TeamSlugInvalid)
            .When(x => !string.IsNullOrWhiteSpace(x.Slug));

        RuleFor(x => x.Slug)
            .Must(slug => !options.ReservedSlugs.Contains(SlugGenerator.NormalizeTypedSlug(slug), StringComparer.OrdinalIgnoreCase))
            .WithMessage("{PropertyName} is reserved.")
            .WithErrorCode(ErrorCodes.TeamSlugReserved)
            .When(x => !string.IsNullOrWhiteSpace(x.Slug));
    }
}
