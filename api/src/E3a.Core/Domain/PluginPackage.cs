namespace E3a.Core.Domain;

/// <summary>The fully assembled, in-memory file tree of a plugin, ready to scan and zip.</summary>
public sealed record PluginPackage(string PluginName, string SemanticVersion, IReadOnlyList<PluginFile> Files)
{
    public PluginFile? Find(string relativePath)
    {
        return Files.FirstOrDefault(f => string.Equals(f.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>One file inside a plugin. Paths are forward-slash relative, no leading slash.</summary>
public sealed record PluginFile(string RelativePath, byte[] Content)
{
    public bool IsText => TextExtensions.Contains(Path.GetExtension(RelativePath).ToLowerInvariant());

    public string AsText()
    {
        return System.Text.Encoding.UTF8.GetString(Content);
    }

    private static readonly HashSet<string> TextExtensions =
        [".md", ".json", ".txt", ".yml", ".yaml", ".toml", ".xml", ".csv"];
}
