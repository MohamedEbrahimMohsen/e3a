using Core.DDD.Models;
using FluentValidation;
using Microsoft.AspNetCore.Http;

namespace Core.Validation.Extensions;

public static class RequiredValidationExtensions
{
    private const string DefaultMessage = "{PropertyName} is required.";

    public static IRuleBuilderOptions<T, DateTimeOffset> ValidateRequired<T>(this IRuleBuilder<T, DateTimeOffset> ruleBuilder, string? errorCode = null)
    => ruleBuilder
        .NotNull()
        //.Must(date => date.HasValue && date.Value != default)
        .Must(date => date != default)
        .WithMessage(DefaultMessage)
        .WithErrorCode(errorCode ?? ValidationErrors.ValidationRequired);

    public static IRuleBuilderOptions<T, string?> ValidateRequired<T>(this IRuleBuilder<T, string?> ruleBuilder, string? errorCode = null)
        => ruleBuilder
            .NotEmpty()
            .WithMessage(DefaultMessage)
            .WithErrorCode(errorCode ?? ValidationErrors.ValidationRequired);

    public static IRuleBuilderOptions<T, TValue?> ValidateRequired<T, TValue>(this IRuleBuilder<T, TValue?> ruleBuilder, string? errorCode = null) where TValue : struct
    {
        return ruleBuilder
            .NotNull()
            .WithMessage(DefaultMessage)
            .WithErrorCode(errorCode ?? ValidationErrors.ValidationRequired);
    }

    public static IRuleBuilderOptions<T, Guid> ValidateRequired<T>(this IRuleBuilder<T, Guid> ruleBuilder, string? errorCode = null)
        => ruleBuilder
            .NotEmpty()
            .WithMessage(DefaultMessage)
            .WithErrorCode(errorCode ?? ValidationErrors.ValidationRequired);

    public static IRuleBuilderOptions<T, IFormFile?> ValidateRequired<T>(this IRuleBuilder<T, IFormFile?> ruleBuilder, string? errorCode = null)
        => ruleBuilder
            .NotNull()
            .Must(file => file!.Length > 0)
            .WithMessage(DefaultMessage)
            .WithErrorCode(errorCode ?? ValidationErrors.ValidationRequired);

    public static IRuleBuilderOptions<T, LocalizedText?> ValidateRequired<T>(this IRuleBuilder<T, LocalizedText?> ruleBuilder, string? arabicErrorCode = null, string? englishErrorCode = null)
    => ruleBuilder
        .NotNull()
        .NotEmpty()
        .ChildRules(localizedText =>
        {
            localizedText.RuleFor(x => x.English)
                .NotEmpty()
                .WithMessage(DefaultMessage)
                .WithErrorCode(englishErrorCode ?? ValidationErrors.ValidationRequired);

            localizedText.RuleFor(x => x.Arabic)
                .NotEmpty()
                .WithMessage(DefaultMessage)
                .WithErrorCode(arabicErrorCode ?? ValidationErrors.ValidationRequired);
        })
        .WithMessage(DefaultMessage)
        .WithErrorCode(ValidationErrors.ValidationRequired);
}
