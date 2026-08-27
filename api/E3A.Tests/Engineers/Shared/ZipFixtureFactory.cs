using System.IO.Compression;
using System.Text;

namespace E3A.Tests.Engineers.Shared;

public static class ZipFixtureFactory
{
    public static byte[] Build(params (string Path, string Content)[] entries)
    {
        using var stream = new MemoryStream();

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in entries)
            {
                WriteEntry(archive.CreateEntry(path), content);
            }
        }

        return stream.ToArray();
    }

    public static byte[] BuildWithExternalAttributes(string path, string content, int externalAttributes)
    {
        using var stream = new MemoryStream();

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry(path);
            entry.ExternalAttributes = externalAttributes;
            WriteEntry(entry, content);
        }

        return stream.ToArray();
    }

    public static Stream AsStream(byte[] zipBytes)
    {
        return new MemoryStream(zipBytes);
    }

    private static void WriteEntry(ZipArchiveEntry entry, string content)
    {
        using var entryStream = entry.Open();
        entryStream.Write(Encoding.UTF8.GetBytes(content));
    }
}
