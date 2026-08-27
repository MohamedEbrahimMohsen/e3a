using FluentValidation;
using Microsoft.AspNetCore.Http;

namespace Core.Validation.Extensions;

public static class FileValidationExtensions
{
    public static IRuleBuilderOptions<T, IFormFile?> ValidateAllowedExtensions<T>(this IRuleBuilder<T, IFormFile?> ruleBuilder, IEnumerable<string> allowedExtensions, string? errorCode = null)
    {
        var allowed = new HashSet<string>(allowedExtensions.Select(e => e.ToLowerInvariant()));

        return ruleBuilder
            .Must(file =>
                file == null ||
                (!string.IsNullOrWhiteSpace(file.FileName) &&
                 allowed.Contains(Path.GetExtension(file.FileName).ToLowerInvariant())))
            .WithMessage($"Only the following file types are allowed: {string.Join(", ", allowed)}")
            .WithErrorCode(errorCode ?? ValidationErrors.ValidationAllowedExtensions);
    }

    public static IRuleBuilderOptions<T, IFormFile?> ValidateMaxFileSize<T>(this IRuleBuilder<T, IFormFile?> ruleBuilder, int maxFileSizeInMb, string? errorCode = null)
    {
        long maxBytes = maxFileSizeInMb * 1024L * 1024L;

        return ruleBuilder
            .Must(file => file == null || file.Length <= maxBytes)
            .WithMessage($"The file size must not exceed {maxFileSizeInMb} MB.")
            .WithErrorCode(errorCode ?? ValidationErrors.ValidationFileSize);
    }

    public static IRuleBuilderOptions<T, IFormFile?> ValidateFileNameLength<T>(this IRuleBuilder<T, IFormFile?> ruleBuilder, int minLength, int maxLength, string? errorCode = null)
        => ruleBuilder
            .Must(file =>
            {
                if (file == null)
                {
                    return true;
                }

                var name = Path.GetFileNameWithoutExtension(file.FileName);
                return name.Length >= minLength && name.Length <= maxLength;
            })
            .WithMessage($"File name length must be between {minLength} and {maxLength} characters.")
            .WithErrorCode(errorCode ?? ValidationErrors.ValidationMaxLength);
}