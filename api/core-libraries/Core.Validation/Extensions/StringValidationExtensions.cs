using FluentValidation;
using System.Text.RegularExpressions;

namespace Core.Validation.Extensions;

public static class StringValidationExtensions
{
    private static readonly Regex EmailRegex = new(@"^[a-zA-Z0-9._-]+@([a-zA-Z0-9-]+\.)+[a-zA-Z]{2,}$", RegexOptions.Compiled);
    private static readonly Regex ArabicRegex = new(@"[\u0600-\u06FF]", RegexOptions.Compiled);
    private static readonly Regex DigitsOnlyRegex = new(@"^[0-9]+$", RegexOptions.Compiled);

    public static IRuleBuilderOptions<T, string?> ValidateMaxLength<T>( this IRuleBuilder<T, string?> ruleBuilder, int maxLength, string? errorCode = null)
        => ruleBuilder
            .MaximumLength(maxLength)
            .WithMessage($"{{PropertyName}} must not exceed {maxLength} characters.")
            .WithErrorCode(errorCode ?? ValidationErrors.ValidationMaxLength)
            .When(x => !string.IsNullOrWhiteSpace(x?.ToString()));

    public static IRuleBuilderOptions<T, string?> ValidateMinLength<T>(this IRuleBuilder<T, string?> ruleBuilder, int minLength, string? errorCode = null)
    => ruleBuilder
        .MinimumLength(minLength)
        .WithMessage($"{{PropertyName}} must be at least {minLength} characters.")
        .WithErrorCode(errorCode ?? ValidationErrors.ValidationMinLength)
        .When(x => !string.IsNullOrWhiteSpace(x?.ToString()));

    public static IRuleBuilderOptions<T, string?> ValidateEmail<T>(this IRuleBuilder<T, string?> ruleBuilder, string? errorCode = null)
        => ruleBuilder
            .Matches(EmailRegex)
            .WithMessage("{PropertyName} must be a valid email address containing only English letters.")
            .WithErrorCode(errorCode ?? ValidationErrors.ValidationEmail)
            .When(x => !string.IsNullOrWhiteSpace(x?.ToString()));

    public static IRuleBuilderOptions<T, string?> ValidateUrl<T>(this IRuleBuilder<T, string?> ruleBuilder, string? errorCode = null)
        => ruleBuilder
            .Must(url =>
                Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
                !ArabicRegex.IsMatch(url))
            .WithMessage("{PropertyName} must be a valid URL containing only English characters.")
            .WithErrorCode(errorCode ?? ValidationErrors.ValidationUrl)
            .When(x => !string.IsNullOrWhiteSpace(x?.ToString()));

    public static IRuleBuilderOptions<T, string?> ValidateOnlyDigits<T>(this IRuleBuilder<T, string?> ruleBuilder, string? errorCode = null)
        => ruleBuilder
            .Matches(DigitsOnlyRegex)
            .WithMessage("{PropertyName} accepts only digits.")
            .WithErrorCode(errorCode ?? ValidationErrors.ValidationOnlyDigits);

    public static IRuleBuilderOptions<T, string?> ValidatePhoneNumber<T>(this IRuleBuilder<T, string?> ruleBuilder, IEnumerable<string> phoneCodes, int phoneLength, string? errorCode = null)
    {
        var codes = phoneCodes?.ToList() ?? [];
        return ruleBuilder
            .ValidateRequired(ValidationErrors.ValidationPhoneNumberIsRequired)
            .ValidateOnlyDigits(ValidationErrors.ValidationPhoneNumberMustBeOnlyDigits)
            .Length(phoneLength).WithMessage(ValidationErrors.ValidationPhoneNumberMustBeXDigits)
            .Must(phone => !string.IsNullOrEmpty(phone) && codes.Any(code => phone.StartsWith(code)))
                .WithErrorCode(errorCode ?? ValidationErrors.ValidationPhoneNumberInvalidCellulerCode);
    }
}