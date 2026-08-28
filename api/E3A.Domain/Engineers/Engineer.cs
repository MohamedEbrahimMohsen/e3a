using Core.DDD.Entities;

namespace E3A.Domain.Engineers;

public class Engineer : AuditEntity
{
    public Guid OwnerUserId { get; private set; }
    public string Slug { get; private set; } = default!;
    public string DisplayName { get; private set; } = default!;
    public string? Description { get; private set; }
    public List<string> Tags { get; private set; } = [];
    public EngineerStatus Status { get; private set; }
    public string? DraftManifestJson { get; private set; }
    public Guid? LatestVersionId { get; private set; }
    public int InstallCount { get; private set; }
    public bool IsSlugMutable => LatestVersionId == null;

    private Engineer(Guid id, Guid? createdBy) : base(id, createdBy) { }

    public static Engineer Create(Guid ownerUserId, string slug, string displayName, string? description, List<string> tags)
    {
        return new Engineer(Guid.NewGuid(), ownerUserId)
        {
            OwnerUserId = ownerUserId,
            Slug = slug,
            DisplayName = displayName,
            Description = description,
            Tags = [.. tags],
            Status = EngineerStatus.Draft,
            DraftManifestJson = null,
            LatestVersionId = null,
            InstallCount = 0,
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

    public void MarkPublished(Guid latestVersionId)
    {
        Status = EngineerStatus.Published;
        LatestVersionId = latestVersionId;
        UpdationDate = DateTimeOffset.UtcNow;
    }

    public void RecordInstallCount(int installCount)
    {
        InstallCount = installCount;
        UpdationDate = DateTimeOffset.UtcNow;
    }

    public void ReplaceDraftManifest(string draftManifestJson)
    {
        DraftManifestJson = draftManifestJson;
        UpdationDate = DateTimeOffset.UtcNow;
    }

    public void Delete()
    {
        Status = EngineerStatus.Deleted;
        SoftDelete();
        UpdationDate = DateTimeOffset.UtcNow;
    }
}
