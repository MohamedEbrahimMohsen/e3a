using FluentValidation;
using System.Text.RegularExpressions;

namespace Core.Validation.Extensions;

public static class LocalizedTextValidationExtensions
{
    private static readonly Regex EnglishAlphanumericWithSpacesRegex = new(@"^[A-Za-z0-9 ]+$", RegexOptions.Compiled);
    private static readonly Regex EnglishAlphanumericRegex = new(@"^[A-Za-z0-9]+$", RegexOptions.Compiled);
    private static readonly Regex ArabicAlphanumericWithSpacesRegex = new(@"^[\u0600-\u06FF0-9 ]+$", RegexOptions.Compiled);
    private static readonly Regex ArabicAlphanumericRegex = new(@"^[\u0600-\u06FF0-9]+$", RegexOptions.Compiled);

    public static IRuleBuilderOptions<T, string?> ValidateEnglishAlphanumericWithSpaces<T>(this IRuleBuilder<T, string?> ruleBuilder, string? errorCode = null)
        => ruleBuilder
            .Matches(EnglishAlphanumericWithSpacesRegex)
            .WithMessage("{PropertyName} must contain only English letters, numbers, and spaces.")
            .WithErrorCode(errorCode ?? ValidationErrors.ValidationInvalidCharacters)
            .When(x => !string.IsNullOrWhiteSpace(x?.ToString()));

    public static IRuleBuilderOptions<T, string?> ValidateEnglishAlphanumeric<T>(this IRuleBuilder<T, string?> ruleBuilder, string? errorCode = null)
        => ruleBuilder
            .Matches(EnglishAlphanumericRegex)
            .WithMessage("{PropertyName} must contain only English letters and numbers.")
            .WithErrorCode(errorCode ?? ValidationErrors.ValidationInvalidCharacters)
            .When(x => !string.IsNullOrWhiteSpace(x?.ToString()));

    public static IRuleBuilderOptions<T, string?> ValidateArabicAlphanumericWithSpaces<T>(this IRuleBuilder<T, string?> ruleBuilder, string? errorCode = null)
        => ruleBuilder
            .Matches(ArabicAlphanumericWithSpacesRegex)
            .WithMessage("{PropertyName} must contain only Arabic letters, numbers, and spaces.")
            .WithErrorCode(errorCode ?? ValidationErrors.ValidationInvalidCharacters)
            .When(x => !string.IsNullOrWhiteSpace(x?.ToString()));

    public static IRuleBuilderOptions<T, string?> ValidateArabicAlphanumeric<T>(this IRuleBuilder<T, string?> ruleBuilder, string? errorCode = null)
        => ruleBuilder
            .Matches(ArabicAlphanumericRegex)
            .WithMessage("{PropertyName} must contain only Arabic letters and numbers.")
            .WithErrorCode(errorCode ?? ValidationErrors.ValidationInvalidCharacters)
            .When(x => !string.IsNullOrWhiteSpace(x?.ToString()));
}
