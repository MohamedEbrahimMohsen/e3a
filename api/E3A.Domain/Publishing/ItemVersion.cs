using Core.DDD.Entities;

namespace E3A.Domain.Publishing;

public class ItemVersion : AuditEntity
{
    public ItemType ItemType { get; private set; }
    public Guid ItemId { get; private set; }
    public int VersionNumber { get; private set; }
    public string SemanticVersion { get; private set; } = default!;
    public string FrozenManifestJson { get; private set; } = default!;
    public ItemVersionStatus Status { get; private set; }
    public string? ZipBlobPath { get; private set; }
    public string? ZipSha256 { get; private set; }
    public long SizeBytes { get; private set; }
    public string? FailureReason { get; private set; }
    public string? ScanReportJson { get; private set; }
    public bool IsTerminal => Status is ItemVersionStatus.Published or ItemVersionStatus.Rejected or ItemVersionStatus.Failed;

    private ItemVersion(Guid id, Guid? createdBy) : base(id, createdBy) { }

    public static ItemVersion Create(ItemType itemType, Guid itemId, int versionNumber, string semanticVersion, string frozenManifestJson, Guid createdBy)
    {
        var version = new ItemVersion(Guid.NewGuid(), createdBy)
        {
            ItemType = itemType,
            ItemId = itemId,
            VersionNumber = versionNumber,
            SemanticVersion = semanticVersion,
            FrozenManifestJson = frozenManifestJson,
            Status = ItemVersionStatus.Queued,
            SizeBytes = 0,
            CreationDate = DateTimeOffset.UtcNow,
            UpdationDate = DateTimeOffset.UtcNow,
        };

        version.RaiseDomainEvent(new PublishRequestedDomainEvent(version.Id));

        return version;
    }

    public void MarkBuilding()
    {
        Status = ItemVersionStatus.Building;
        UpdationDate = DateTimeOffset.UtcNow;
    }

    public void MarkPublished(string zipBlobPath, string zipSha256, long sizeBytes)
    {
        Status = ItemVersionStatus.Published;
        ZipBlobPath = zipBlobPath;
        ZipSha256 = zipSha256;
        SizeBytes = sizeBytes;
        FailureReason = null;
        UpdationDate = DateTimeOffset.UtcNow;
    }

    public void RecordScanReport(string scanReportJson)
    {
        ScanReportJson = scanReportJson;
        UpdationDate = DateTimeOffset.UtcNow;
    }

    public void MarkRejected(string failureReason)
    {
        Status = ItemVersionStatus.Rejected;
        FailureReason = failureReason;
        UpdationDate = DateTimeOffset.UtcNow;
    }

    public void MarkFailed(string failureReason)
    {
        Status = ItemVersionStatus.Failed;
        FailureReason = failureReason;
        UpdationDate = DateTimeOffset.UtcNow;
    }
}
