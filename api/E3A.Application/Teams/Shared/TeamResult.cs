namespace E3A.Application.Teams.Shared;

public sealed record TeamResult(Guid Id, string Slug, string DisplayName, string? Description, List<string> Tags, string Status, Guid? LatestVersionId, int MemberCount, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record TeamMemberResult(Guid EngineerId, string EngineerSlug, Guid PinnedVersionId, string PinnedSemanticVersion, int SortOrder);

public sealed record TeamDetailResult(Guid Id, string Slug, string DisplayName, string? Description, List<string> Tags, string Status, Guid? LatestVersionId, List<TeamMemberResult> Members, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
