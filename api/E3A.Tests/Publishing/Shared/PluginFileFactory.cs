using System.Text;
using E3A.Application.Engineers.Shared;
using E3A.Application.Publishing.Shared;

namespace E3A.Tests.Publishing.Shared;

public static class PluginFileFactory
{
    public const string DefaultCategory = "agents";

    public static ImportManifestResult Manifest(params string[] targetPaths)
    {
        return new ImportManifestResult([.. targetPaths.Select(x => new ImportedItemResult($".claude/{x}", x, DefaultCategory))], [], [], [], [], null, DateTimeOffset.UtcNow);
    }

    public static ImportManifestResult ConvertingManifest(params string[] targetPaths)
    {
        return new ImportManifestResult([], [.. targetPaths.Select(x => new ConvertedItemResult(".claude/CLAUDE.md", x, "Merged into the generated house-rules skill."))], [], [], [], null, DateTimeOffset.UtcNow);
    }

    public static List<PluginFile> Files(params string[] paths)
    {
        return [.. paths.Select(x => new PluginFile(x, Encoding.UTF8.GetBytes($"content of {x}")))];
    }
}
