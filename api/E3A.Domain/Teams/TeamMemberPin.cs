namespace E3A.Domain.Teams;

public sealed record TeamMemberPin(Guid EngineerId, string EngineerSlug, Guid PinnedVersionId, string PinnedSemanticVersion);
