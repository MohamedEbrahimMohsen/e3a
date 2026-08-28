using E3A.Application.Engineers.Shared;
using E3A.Application.Exceptions;
using E3A.Application.Options;

namespace E3A.Application.Publishing.Shared;

public static class PluginStructureValidator
{
    // Claude Code loads plugin content only from these roots; a tree with none of them installs but does nothing.
    private static readonly string[] InstallableRoots = ["agents/", "skills/", "commands/"];

    // A skill is addressed as skills/{folder}/SKILL.md; without that file the folder cannot be loaded.
    private const string SkillsRoot = "skills/";
    private const string SkillFileName = "SKILL.md";
    private const string ParentDirectorySegment = "..";

    public static List<string> Validate(List<PluginFile> files, ImportManifestResult manifest, PublishingOptions options)
    {
        var allowed = new HashSet<string>(manifest.Imported.Select(x => x.TargetPath).Concat(manifest.Converted.Select(x => x.TargetPath)), StringComparer.OrdinalIgnoreCase);
        var paths = new HashSet<string>(files.Select(x => x.Path), StringComparer.OrdinalIgnoreCase);
        List<string> errors = [];

        if (allowed.Any(targetPath => !paths.Contains(targetPath)))
        {
            errors.Add(ErrorCodes.PluginManifestAssetMissing);
        }

        if (!files.Exists(file => Array.Exists(InstallableRoots, root => file.Path.StartsWith(root, StringComparison.OrdinalIgnoreCase))))
        {
            errors.Add(ErrorCodes.PluginNoInstallableContent);
        }

        if (files.Exists(file => IsUnsafePath(file.Path)))
        {
            errors.Add(ErrorCodes.PluginUnsafePath);
        }

        if (SkillFolders(files).Any(folder => !paths.Contains(folder + SkillFileName)))
        {
            errors.Add(ErrorCodes.PluginSkillMissingSkillFile);
        }

        if (files.Count > options.MaxPluginFileCount)
        {
            errors.Add(ErrorCodes.PluginTooManyFiles);
        }

        if (files.Sum(x => x.Content.LongLength) > options.MaxPluginBytes)
        {
            errors.Add(ErrorCodes.PluginTooLarge);
        }

        return errors;
    }

    private static bool IsUnsafePath(string path)
    {
        return string.IsNullOrWhiteSpace(path)
            || path.StartsWith('/')
            || path.Contains('\\', StringComparison.Ordinal)
            || path.Split('/').Contains(ParentDirectorySegment, StringComparer.Ordinal);
    }

    private static IEnumerable<string> SkillFolders(List<PluginFile> files)
    {
        return files
            .Where(x => x.Path.StartsWith(SkillsRoot, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Path.Split('/'))
            .Where(x => x.Length > 2)
            .Select(x => $"{x[0]}/{x[1]}/")
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
