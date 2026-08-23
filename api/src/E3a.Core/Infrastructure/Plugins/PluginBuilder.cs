using System.IO.Compression;
using System.Security.Cryptography;
using E3a.Core.Domain;

namespace E3a.Core.Infrastructure.Plugins;

/// <summary>Builds deterministic plugin zips: same input files → byte-identical zip → stable sha256.</summary>
public sealed class PluginBuilder
{
    // Fixed timestamp so rebuilding an identical package yields an identical archive.
    private static readonly DateTimeOffset FixedTimestamp = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public BuiltPlugin Build(PluginPackage package)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in package.Files.OrderBy(f => f.RelativePath, StringComparer.Ordinal))
            {
                var entry = archive.CreateEntry(file.RelativePath, CompressionLevel.Optimal);
                entry.LastWriteTime = FixedTimestamp;
                using var entryStream = entry.Open();
                entryStream.Write(file.Content);
            }
        }

        var bytes = stream.ToArray();
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
        return new BuiltPlugin(package.PluginName, package.SemanticVersion, bytes, sha256);
    }
}

public sealed record BuiltPlugin(string PluginName, string SemanticVersion, byte[] ZipBytes, string Sha256)
{
    public string GetBlobPath(string zipPathPrefix)
    {
        return $"{zipPathPrefix}/{PluginName}/{SemanticVersion}.zip";
    }
}
