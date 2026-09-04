using System.Text;
using System.Text.RegularExpressions;

namespace E3A.Domain.SharedKernel;

// Kebab-case normalization only — uniqueness suffixes come from Core.Utilities IGenerator.
public static class SlugGenerator
{
    // A match timeout is mandatory (Sonar S6444); the pattern cannot backtrack, so any bound suffices.
    private static readonly TimeSpan SlugFormatMatchTimeout = TimeSpan.FromMilliseconds(100);
    private static readonly Regex SlugFormatRegex = new("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled, SlugFormatMatchTimeout);

    public static string Normalize(string displayName, int maxLength)
    {
        var builder = new StringBuilder();

        foreach (var character in displayName)
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var slug = builder.ToString().TrimEnd('-');

        if (slug.Length > maxLength)
        {
            slug = slug[..maxLength].TrimEnd('-');
        }

        return slug;
    }

    public static string NormalizeTypedSlug(string? slug)
    {
        return slug?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    public static bool IsValidFormat(string slug)
    {
        return SlugFormatRegex.IsMatch(slug);
    }
}
