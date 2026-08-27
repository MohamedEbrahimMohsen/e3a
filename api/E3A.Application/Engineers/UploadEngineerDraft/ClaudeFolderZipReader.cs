using System.IO.Compression;
using Core.Errors;
using E3A.Application.Exceptions;
using E3A.Application.Options;

namespace E3A.Application.Engineers.UploadEngineerDraft;

public static class ClaudeFolderZipReader
{
    // Zip external attributes carry the unix mode in the high 16 bits; file type 0xA000 is a symbolic link.
    private const int UnixModeShift = 16;
    private const int UnixFileTypeMask = 0xF000;
    private const int UnixSymbolicLinkType = 0xA000;

    // The size cap is enforced per read so a zip bomb is never fully materialised in memory.
    private const int ReadBufferSizeBytes = 81920;

    public static List<UploadedFile> Read(Stream zipStream, UploadsOptions options)
    {
        using var seekableStream = new MemoryStream();
        zipStream.CopyTo(seekableStream);
        seekableStream.Position = 0;

        try
        {
            return ReadEntries(seekableStream, options);
        }
        catch (InvalidDataException exception)
        {
            throw new BadRequestCoreException(ErrorCodes.UploadZipInvalid, innerException: exception);
        }
    }

    private static List<UploadedFile> ReadEntries(Stream seekableStream, UploadsOptions options)
    {
        using var archive = new ZipArchive(seekableStream, ZipArchiveMode.Read);
        List<UploadedFile> files = [];
        var totalBytes = 0L;

        foreach (var entry in archive.Entries)
        {
            var path = entry.FullName.Replace('\\', '/');

            if (path.Length == 0 || path.EndsWith('/'))
            {
                continue;
            }

            if (Path.IsPathRooted(path) || path.Split('/').Any(segment => segment == ".."))
            {
                throw new BadRequestCoreException(ErrorCodes.UploadUnsafePath, context: new Dictionary<string, object> { ["path"] = path });
            }

            if (((entry.ExternalAttributes >> UnixModeShift) & UnixFileTypeMask) == UnixSymbolicLinkType)
            {
                throw new BadRequestCoreException(ErrorCodes.UploadSymlinkNotAllowed, context: new Dictionary<string, object> { ["path"] = path });
            }

            if (files.Count >= options.MaxFileCount)
            {
                throw new BadRequestCoreException(ErrorCodes.UploadTooManyFiles, context: new Dictionary<string, object> { ["limit"] = options.MaxFileCount });
            }

            files.Add(new UploadedFile(path, ReadContent(entry, options, ref totalBytes)));
        }

        return files;
    }

    private static byte[] ReadContent(ZipArchiveEntry entry, UploadsOptions options, ref long totalBytes)
    {
        using var entryStream = entry.Open();
        using var contentStream = new MemoryStream();
        var buffer = new byte[ReadBufferSizeBytes];
        int read;

        while ((read = entryStream.Read(buffer, 0, buffer.Length)) > 0)
        {
            totalBytes += read;

            if (totalBytes > options.MaxUncompressedSizeBytes)
            {
                throw new BadRequestCoreException(ErrorCodes.UploadUncompressedTooLarge, context: new Dictionary<string, object> { ["limit"] = options.MaxUncompressedSizeBytes });
            }

            contentStream.Write(buffer, 0, read);
        }

        return contentStream.ToArray();
    }
}
