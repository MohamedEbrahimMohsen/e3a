using Core.DDD.Entities;

namespace E3A.Domain.Teams;

public class TeamMember : AuditEntity
{
    public Guid TeamId { get; private set; }
    public Guid EngineerId { get; private set; }
    public string EngineerSlug { get; private set; } = default!;
    public Guid PinnedVersionId { get; private set; }
    public string PinnedSemanticVersion { get; private set; } = default!;
    public int SortOrder { get; private set; }

    private TeamMember(Guid id, Guid? createdBy) : base(id, createdBy) { }

    public static TeamMember Create(Guid teamId, TeamMemberPin pin, int sortOrder, Guid createdBy)
    {
        return new TeamMember(Guid.NewGuid(), createdBy)
        {
            TeamId = teamId,
            EngineerId = pin.EngineerId,
            EngineerSlug = pin.EngineerSlug,
            PinnedVersionId = pin.PinnedVersionId,
            PinnedSemanticVersion = pin.PinnedSemanticVersion,
            SortOrder = sortOrder,
            CreationDate = DateTimeOffset.UtcNow,
            UpdationDate = DateTimeOffset.UtcNow,
        };
    }
}
