using Core.Errors;
using E3A.Application.Exceptions;
using E3A.Application.Options;

namespace E3A.Application.Engineers.UploadEngineerDraft;

public static class UploadPathNormalizer
{
    // Plugin-format roots (docs/plugin-spec.md): a zip whose single root is one of these is already unwrapped.
    private static readonly HashSet<string> RecognizedRootNames = new(StringComparer.OrdinalIgnoreCase) { "skills", "agents", "commands", "hooks", "output-styles", "monitors", "bin", "themes", "rules", "conventions", "docs" };

    // The folder the creator actually zips; a repository zip keeps it beside the root CLAUDE.md.
    private const string ClaudeFolderPrefix = ".claude/";
    private const string CurrentFolderPrefix = "./";

    public static List<UploadedFile> Normalize(List<UploadedFile> files, UploadsOptions options)
    {
        var normalized = files
            .Select(file => file with { Path = TrimPrefix(file.Path, CurrentFolderPrefix) })
            .ToList();

        normalized = Unwrap(normalized);
        normalized = normalized
            .Select(file => file with { Path = TrimPrefix(file.Path, ClaudeFolderPrefix) })
            .ToList();

        HashSet<string> seenPaths = new(StringComparer.OrdinalIgnoreCase);

        foreach (var path in normalized.Select(file => file.Path))
        {
            if (!seenPaths.Add(path))
            {
                throw new BadRequestCoreException(ErrorCodes.UploadDuplicatePath, context: new Dictionary<string, object> { ["path"] = path });
            }

            if (!options.AllowedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            {
                throw new BadRequestCoreException(ErrorCodes.UploadFileTypeNotAllowed, context: new Dictionary<string, object> { ["path"] = path });
            }
        }

        return normalized;
    }

    private static List<UploadedFile> Unwrap(List<UploadedFile> files)
    {
        var unwrapped = files;

        while (SingleUnrecognizedRoot(unwrapped) is { } root)
        {
            unwrapped = unwrapped
                .Select(file => file with { Path = file.Path[(root.Length + 1)..] })
                .ToList();
        }

        return unwrapped;
    }

    private static string? SingleUnrecognizedRoot(List<UploadedFile> files)
    {
        if (files.Count == 0)
        {
            return null;
        }

        var separatorIndex = files[0].Path.IndexOf('/');

        if (separatorIndex <= 0)
        {
            return null;
        }

        var root = files[0].Path[..separatorIndex];

        if (RecognizedRootNames.Contains(root) || !files.All(file => file.Path.StartsWith($"{root}/", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        return root;
    }

    private static string TrimPrefix(string path, string prefix)
    {
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? path[prefix.Length..] : path;
    }
}
