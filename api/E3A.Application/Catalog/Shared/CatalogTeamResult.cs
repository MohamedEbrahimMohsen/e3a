namespace E3A.Application.Catalog.Shared;

public sealed record CatalogTeamResult(Guid Id, string Slug, string DisplayName, string? Description, List<string> Tags, List<string> MemberSlugs, Guid? LatestVersionId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
