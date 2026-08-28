using E3A.Application.Engineers.Shared;
using E3A.Application.Options;
using E3A.Domain.Engineers;

namespace E3A.Application.Publishing.Shared;

public static class PluginTreeAssembler
{
    public static List<PluginFile> Assemble(List<PluginFile> snapshotAssets, ImportManifestResult manifest, Engineer engineer, string semanticVersion, string authorName, PublishingOptions options)
    {
        var allowed = new HashSet<string>(manifest.Imported.Select(x => x.TargetPath).Concat(manifest.Converted.Select(x => x.TargetPath)), StringComparer.OrdinalIgnoreCase);

        List<PluginFile> files = [.. snapshotAssets.Where(x => allowed.Contains(x.Path))];
        files.Add(PluginJsonGenerator.Generate(engineer, semanticVersion, authorName, options));

        return [.. files.OrderBy(x => x.Path, StringComparer.Ordinal)];
    }
}
