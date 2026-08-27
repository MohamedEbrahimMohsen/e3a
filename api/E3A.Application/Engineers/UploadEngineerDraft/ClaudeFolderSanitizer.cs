using E3A.Application.Options;

namespace E3A.Application.Engineers.UploadEngineerDraft;

public sealed record SanitizeOutcome(List<UploadedFile> Files, List<string> StrippedPaths);

public static class ClaudeFolderSanitizer
{
    public static SanitizeOutcome Sanitize(List<UploadedFile> files, UploadsOptions options)
    {
        List<UploadedFile> kept = [];
        List<string> strippedPaths = [];

        foreach (var file in files)
        {
            if (ShouldStrip(file.Path, options))
            {
                strippedPaths.Add(file.Path);
                continue;
            }

            kept.Add(file);
        }

        return new SanitizeOutcome(kept, strippedPaths);
    }

    private static bool ShouldStrip(string path, UploadsOptions options)
    {
        var segments = path.Split('/');
        var fileName = segments[^1];

        return options.StrippedFileNames.Any(name => fileName.Equals(name, StringComparison.OrdinalIgnoreCase))
            || options.StrippedFileNamePrefixes.Any(prefix => fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            || segments[..^1].Any(segment => options.StrippedFolderNames.Any(folder => segment.Equals(folder, StringComparison.OrdinalIgnoreCase)));
    }
}
