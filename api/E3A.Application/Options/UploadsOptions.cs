namespace E3A.Application.Options;

public sealed class UploadsOptions
{
    public const string SectionName = "Uploads";

    public int MaxZipSizeMegabytes { get; set; }
    public long MaxUncompressedSizeBytes { get; set; }
    public int MaxFileCount { get; set; }
    public List<string> AllowedExtensions { get; set; } = [];
    public List<string> HookScriptExtensions { get; set; } = [];
    public List<string> StrippedFileNames { get; set; } = [];
    public List<string> StrippedFileNamePrefixes { get; set; } = [];
    public List<string> StrippedFolderNames { get; set; } = [];
}
