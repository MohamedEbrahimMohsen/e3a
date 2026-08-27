using FluentValidation;

namespace Core.Validation.Extensions;

public static class NumericValidationExtensions
{
    public static IRuleBuilderOptions<T, TNumber> ValidatePositive<T, TNumber>(this IRuleBuilder<T, TNumber> ruleBuilder, string? errorCode = null)
        where TNumber : System.Numerics.INumber<TNumber>
        => ruleBuilder
            .Must(property => property > TNumber.Zero)
            .WithMessage("{PropertyName} must be a positive number.")
            .WithErrorCode(errorCode ?? ValidationErrors.ValidationPositive);

    public static IRuleBuilderOptions<T, TNumber> ValidateNonNegative<T, TNumber>(this IRuleBuilder<T, TNumber> ruleBuilder, string? errorCode = null)
        where TNumber : System.Numerics.INumber<TNumber>
        => ruleBuilder
            .Must(property => property >= TNumber.Zero)
            .WithMessage("{PropertyName} must be a non negative number.")
            .WithErrorCode(errorCode ?? ValidationErrors.ValidationNonNegative);

    public static IRuleBuilderOptions<T, TNumber> ValidateMax<T, TNumber>(this IRuleBuilder<T, TNumber> ruleBuilder, TNumber max, string? errorCode = null)
        where TNumber : System.Numerics.INumber<TNumber>
        => ruleBuilder
            .Must(property => property <= max)
            .WithMessage($"{{PropertyName}} must not exceed {max}.")
            .WithErrorCode(errorCode ?? ValidationErrors.ValidationMaxValue);

    public static IRuleBuilderOptions<T, TNumber> ValidateMin<T, TNumber>(this IRuleBuilder<T, TNumber> ruleBuilder, TNumber min, string? errorCode = null)
        where TNumber : System.Numerics.INumber<TNumber>
        => ruleBuilder
            .Must(property => property >= min)
            .WithMessage($"{{PropertyName}} must be at least {min}.")
            .WithErrorCode(errorCode ?? ValidationErrors.ValidationMinValue);

    public static IRuleBuilderOptions<T, TNumber> ValidateRange<T, TNumber>(this IRuleBuilder<T, TNumber> ruleBuilder, TNumber min, TNumber max, string? errorCode = null)
        where TNumber : System.Numerics.INumber<TNumber>
        => ruleBuilder
            .Must(value => value >= min && value <= max)
            .WithMessage($"{{PropertyName}} must be between {min} and {max}.")
            .WithErrorCode(errorCode ?? ValidationErrors.ValidationRange);
}
