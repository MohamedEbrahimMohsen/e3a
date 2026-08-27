namespace E3A.Application.Engineers.Shared;

public sealed record EngineerResult(Guid Id, string Slug, string DisplayName, string? Description, List<string> Tags, string Status, Guid? LatestVersionId, int InstallCount, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
