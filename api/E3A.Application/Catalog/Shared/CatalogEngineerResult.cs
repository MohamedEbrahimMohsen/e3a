namespace E3A.Application.Catalog.Shared;

public sealed record CatalogEngineerResult(Guid Id, string Slug, string DisplayName, string? Description, List<string> Tags, int InstallCount, Guid? LatestVersionId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
