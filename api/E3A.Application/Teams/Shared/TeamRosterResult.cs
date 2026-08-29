namespace E3A.Application.Teams.Shared;

public sealed record TeamRosterResult(List<TeamRosterMemberResult> Members);

public sealed record TeamRosterMemberResult(Guid EngineerId, string EngineerSlug, Guid PinnedVersionId, string PinnedSemanticVersion, int SortOrder);
