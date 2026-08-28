using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace E3A.Application.Publishing.Shared;

public sealed record ZippedPlugin(byte[] Content, string Sha256, long SizeBytes);

public static class DeterministicZipper
{
    // The MS-DOS zip epoch — the earliest stamp a zip entry can carry. A wall-clock stamp would change the sha256 on every run.
    private static readonly DateTimeOffset DeterministicTimestamp = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static ZippedPlugin Create(List<PluginFile> files)
    {
        using var stream = new MemoryStream();

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true, Encoding.UTF8))
        {
            foreach (var file in files.OrderBy(x => x.Path, StringComparer.Ordinal))
            {
                var entry = archive.CreateEntry(file.Path, CompressionLevel.Optimal);
                entry.LastWriteTime = DeterministicTimestamp;

                using var entryStream = entry.Open();
                entryStream.Write(file.Content, 0, file.Content.Length);
            }
        }

        var content = stream.ToArray();

        return new ZippedPlugin(content, Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(), content.LongLength);
    }
}
