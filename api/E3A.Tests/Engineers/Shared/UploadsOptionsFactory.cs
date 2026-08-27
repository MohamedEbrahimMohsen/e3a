using E3A.Application.Options;

namespace E3A.Tests.Engineers.Shared;

public static class UploadsOptionsFactory
{
    public static UploadsOptions Default(int maxFileCount = 400, long maxUncompressedSizeBytes = 104857600)
    {
        return new UploadsOptions
        {
            MaxZipSizeMegabytes = 20,
            MaxUncompressedSizeBytes = maxUncompressedSizeBytes,
            MaxFileCount = maxFileCount,
            AllowedExtensions = [".md", ".markdown", ".txt", ".json", ".yaml", ".yml", ".toml", ".xml", ".csv", ".html", ".css", ".png", ".jpg", ".jpeg", ".svg", ".sh", ".ps1", ".js", ".py"],
            HookScriptExtensions = [".sh", ".ps1", ".js", ".py"],
            StrippedFileNames = ["settings.local.json", ".credentials.json", "history.jsonl", ".ds_store", "thumbs.db", "desktop.ini"],
            StrippedFileNamePrefixes = [".env"],
            StrippedFolderNames = ["memory", "sessions", "session-env", "shell-snapshots", "todos", "logs", "cache", "statsig", "file-history", "projects"],
        };
    }
}
