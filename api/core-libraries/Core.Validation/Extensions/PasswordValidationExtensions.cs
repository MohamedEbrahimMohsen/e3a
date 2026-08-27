using FluentValidation;
using System.Text.RegularExpressions;

namespace Core.Validation.Extensions;

public static class PasswordValidationExtensions
{
    private static readonly Regex UppercaseRegex = new(@"[A-Z]", RegexOptions.Compiled);
    private static readonly Regex LowercaseRegex = new(@"[a-z]", RegexOptions.Compiled);
    private static readonly Regex NumberRegex = new(@"\d", RegexOptions.Compiled);
    private static readonly Regex SpecialRegex = new(@"[!@#$%*\-_+\?]", RegexOptions.Compiled);

    public static IRuleBuilderOptions<T, string?> ValidateHasUppercase<T>(this IRuleBuilder<T, string?> ruleBuilder, string? errorCode = null)
        => ruleBuilder
            .Matches(UppercaseRegex)
            .WithMessage("Password must contain at least one uppercase letter.")
            .WithErrorCode(errorCode ?? ValidationErrors.ValidationPasswordUppercase)
            .When(p => !string.IsNullOrEmpty(p?.ToString()));

    public static IRuleBuilderOptions<T, string?> ValidateHasLowercase<T>(this IRuleBuilder<T, string?> ruleBuilder, string? errorCode = null)
        => ruleBuilder
            .Matches(LowercaseRegex)
            .WithMessage("Password must contain at least one lowercase letter.")
            .WithErrorCode(errorCode ?? ValidationErrors.ValidationPasswordLowercase)
            .When(p => !string.IsNullOrEmpty(p?.ToString()));

    public static IRuleBuilderOptions<T, string?> ValidateHasNumber<T>(this IRuleBuilder<T, string?> ruleBuilder, string? errorCode = null)
        => ruleBuilder
            .Matches(NumberRegex)
            .WithMessage("Password must contain at least one number.")
            .WithErrorCode(errorCode ?? ValidationErrors.ValidationPasswordNumber)
            .When(p => !string.IsNullOrEmpty(p?.ToString()));

    public static IRuleBuilderOptions<T, string?> ValidateHasSpecialCharacter<T>(this IRuleBuilder<T, string?> ruleBuilder, string? errorCode = null)
        => ruleBuilder
            .Matches(SpecialRegex)
            .WithMessage("Password must contain at least one special character (! @ # $ % * - _ + ?).")
            .WithErrorCode(errorCode ?? ValidationErrors.ValidationPasswordSpecialCharacter)
            .When(p => !string.IsNullOrEmpty(p?.ToString()));
}
