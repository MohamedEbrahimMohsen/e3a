using E3A.Application.Publishing.Security;

namespace E3A.Application.Publishing.Shared;

public sealed record PublishStatusResult(Guid VersionId, Guid EngineerId, int VersionNumber, string SemanticVersion, string Status, string? ZipUrl, string? ZipSha256, long SizeBytes, string? FailureReason, ScanReport? ScanReport, DateTimeOffset UpdatedAt);
