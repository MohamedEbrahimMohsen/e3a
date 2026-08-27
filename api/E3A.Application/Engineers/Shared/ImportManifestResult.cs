namespace E3A.Application.Engineers.Shared;

public sealed record ImportManifestResult(List<ImportedItemResult> Imported, List<ConvertedItemResult> Converted, List<SkippedItemResult> Skipped, List<string> StrippedPaths, List<HookWarningResult> HookWarnings, string? ClaudeMdSnippet, DateTimeOffset UploadedAt);

public sealed record ImportedItemResult(string SourcePath, string TargetPath, string Category);

public sealed record ConvertedItemResult(string SourcePath, string TargetPath, string Reason);

public sealed record SkippedItemResult(string SourcePath, string Reason);

public sealed record HookWarningResult(string Event, string? Matcher, string? Command);
