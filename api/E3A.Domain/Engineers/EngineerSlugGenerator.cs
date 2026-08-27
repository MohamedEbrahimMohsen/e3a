using System.Text;

namespace E3A.Domain.Engineers;

// Kebab-case normalization only — uniqueness suffixes come from Core.Utilities IGenerator.
public static class EngineerSlugGenerator
{
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
}
