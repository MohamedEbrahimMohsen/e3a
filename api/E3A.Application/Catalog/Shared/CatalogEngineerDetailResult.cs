using E3A.Application.Engineers.Shared;

namespace E3A.Application.Catalog.Shared;

public sealed record CatalogEngineerDetailResult(Guid Id, string Slug, string DisplayName, string? Description, List<string> Tags, int InstallCount, Guid OwnerUserId, Guid? LatestVersionId, List<HookWarningResult> HookWarnings, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
