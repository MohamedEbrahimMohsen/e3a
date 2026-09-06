namespace E3A.Application.Catalog.Shared;

public sealed record CreatorProfileResult(Guid Id, string GitHubLogin, string? DisplayName, string? AvatarUrl, DateTimeOffset CreatedAt, int TotalInstalls, List<CatalogEngineerResult> Engineers, List<CatalogTeamResult> Teams);
