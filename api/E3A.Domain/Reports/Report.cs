using Core.DDD.Entities;
using E3A.Domain.Publishing;

namespace E3A.Domain.Reports;

public class Report : AuditEntity
{
    public ItemType ItemType { get; private set; }
    public Guid ItemId { get; private set; }
    public Guid? ReporterUserId { get; private set; }
    public ReportReason Reason { get; private set; }
    public string? Details { get; private set; }
    public ReportStatus Status { get; private set; }
    public bool IsAnonymous => ReporterUserId == null;

    private Report(Guid id, Guid? createdBy) : base(id, createdBy) { }

    public static Report Create(ItemType itemType, Guid itemId, Guid? reporterUserId, ReportReason reason, string? details)
    {
        return new Report(Guid.NewGuid(), reporterUserId)
        {
            ItemType = itemType,
            ItemId = itemId,
            ReporterUserId = reporterUserId,
            Reason = reason,
            Details = details,
            Status = ReportStatus.Open,
            CreationDate = DateTimeOffset.UtcNow,
            UpdationDate = DateTimeOffset.UtcNow,
        };
    }
}
