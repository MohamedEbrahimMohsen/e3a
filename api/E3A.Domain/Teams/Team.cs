using Core.DDD.Entities;

namespace E3A.Domain.Teams;

public class Team : AuditEntity
{
    public Guid OwnerUserId { get; private set; }
    public string Slug { get; private set; } = default!;
    public string DisplayName { get; private set; } = default!;
    public string? Description { get; private set; }
    public List<string> Tags { get; private set; } = [];
    public TeamStatus Status { get; private set; }
    public Guid? LatestVersionId { get; private set; }
    public List<TeamMember> Members { get; private set; } = [];
    public bool IsSlugMutable => LatestVersionId == null;

    private Team(Guid id, Guid? createdBy) : base(id, createdBy) { }

    public static Team Create(Guid ownerUserId, string slug, string displayName, string? description, List<string> tags)
    {
        return new Team(Guid.NewGuid(), ownerUserId)
        {
            OwnerUserId = ownerUserId,
            Slug = slug,
            DisplayName = displayName,
            Description = description,
            Tags = [.. tags],
            Status = TeamStatus.Draft,
            LatestVersionId = null,
            CreationDate = DateTimeOffset.UtcNow,
            UpdationDate = DateTimeOffset.UtcNow,
        };
    }

    public void UpdateMetadata(string displayName, string? description, List<string> tags)
    {
        DisplayName = displayName;
        Description = description;
        Tags = [.. tags];
        UpdationDate = DateTimeOffset.UtcNow;
    }

    public void ChangeSlug(string slug)
    {
        Slug = slug;
        UpdationDate = DateTimeOffset.UtcNow;
    }

    public void ReplaceMembers(List<TeamMemberPin> pins, Guid updatedBy)
    {
        Members.Clear();

        for (var index = 0; index < pins.Count; index++)
        {
            Members.Add(TeamMember.Create(Id, pins[index], index, updatedBy));
        }

        UpdationDate = DateTimeOffset.UtcNow;
    }

    public void MarkPublished(Guid latestVersionId)
    {
        Status = TeamStatus.Published;
        LatestVersionId = latestVersionId;
        UpdationDate = DateTimeOffset.UtcNow;
    }

    public void Delete()
    {
        Status = TeamStatus.Deleted;
        SoftDelete();
        UpdationDate = DateTimeOffset.UtcNow;
    }
}
