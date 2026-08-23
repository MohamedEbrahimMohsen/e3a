using E3a.Core.Domain;
using E3a.Core.Options;
using Microsoft.Extensions.Options;

namespace E3a.Core.Infrastructure.Validation;

public sealed class StructureValidator(IOptions<PublishingOptions> publishingOptions)
{
    private readonly PublishingOptions options = publishingOptions.Value;

    public IReadOnlyList<string> Validate(PluginPackage package)
    {
        var errors = new List<string>();
        var allowedExtensions = options.AllowedSkillExtensions.Select(e => e.ToLowerInvariant()).ToHashSet();

        if (package.Find(".claude-plugin/plugin.json") is null)
        {
            errors.Add("Missing .claude-plugin/plugin.json.");
        }

        foreach (var file in package.Files)
        {
            var path = file.RelativePath;
            if (Path.IsPathRooted(path) || path.Contains("..") || path.Contains('\\'))
            {
                errors.Add($"Unsafe path: {path}");
            }

            var extension = Path.GetExtension(path).ToLowerInvariant();
            if (path.StartsWith("skills/", StringComparison.Ordinal) && !allowedExtensions.Contains(extension))
            {
                errors.Add($"Disallowed file type '{extension}': {path}");
            }
        }

        foreach (var group in package.Files.Where(f => f.RelativePath.StartsWith("skills/", StringComparison.Ordinal)).GroupBy(f => f.RelativePath.Split('/')[1]))
        {
            var slug = group.Key;
            if (!SlugIsValid(slug))
            {
                errors.Add($"Invalid skill slug '{slug}' (kebab-case required).");
            }

            if (group.Count() > options.MaxFilesPerSkill)
            {
                errors.Add($"Skill '{slug}' has {group.Count()} files (max {options.MaxFilesPerSkill}).");
            }

            var totalBytes = group.Sum(f => (long)f.Content.Length);
            if (totalBytes > options.MaxBytesPerSkill)
            {
                errors.Add($"Skill '{slug}' is {totalBytes / 1024} KB (max {options.MaxBytesPerSkill / 1024} KB).");
            }

            var skillMd = group.FirstOrDefault(f => f.RelativePath.Equals($"skills/{slug}/SKILL.md", StringComparison.Ordinal));
            if (skillMd is null)
            {
                errors.Add($"Skill '{slug}' is missing SKILL.md at its root.");
            }
            else
            {
                errors.AddRange(ValidateFrontmatter(slug, skillMd.AsText()));
            }
        }

        return errors;
    }

    private static IEnumerable<string> ValidateFrontmatter(string slug, string skillMd)
    {
        if (!skillMd.StartsWith("---", StringComparison.Ordinal))
        {
            yield return $"Skill '{slug}': SKILL.md must start with YAML frontmatter (---).";
            yield break;
        }

        var end = skillMd.IndexOf("\n---", 3, StringComparison.Ordinal);
        var frontmatter = end < 0 ? skillMd : skillMd[..end];
        if (!frontmatter.Contains("name:"))
        {
            yield return $"Skill '{slug}': frontmatter is missing 'name:'.";
        }

        if (!frontmatter.Contains("description:"))
        {
            yield return $"Skill '{slug}': frontmatter is missing 'description:'.";
        }
    }

    private bool SlugIsValid(string slug)
    {
        var isValid = slug.Length > 0 && slug.Length <= options.MaxSkillSlugLength && slug.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-') && !slug.StartsWith('-') && !slug.EndsWith('-');
        return isValid;
    }
}
