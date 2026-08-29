namespace E3A.Application.Publishing.Shared;

public sealed record PublishStatusResult(Guid VersionId, Guid ItemId, string ItemType, int VersionNumber, string SemanticVersion, string Status, string? ZipUrl, string? ZipSha256, long SizeBytes, string? FailureReason, DateTimeOffset UpdatedAt);
