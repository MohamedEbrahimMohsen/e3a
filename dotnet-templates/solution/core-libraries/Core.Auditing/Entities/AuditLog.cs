using Core.DDD.Entities;

namespace Core.Auditing.Entities;

public class AuditLog : Entity
{
    public DateTimeOffset Timestamp { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public string? ActorUserName { get; private set; }
    public string? ActorRole { get; private set; }
    public string Action { get; private set; } = default!;
    public string ResourceType { get; private set; } = default!;
    public Guid? ResourceId { get; private set; }
    public string Outcome { get; private set; } = default!;
    public string? ErrorCode { get; private set; }
    public string? TraceId { get; private set; }

    private AuditLog() : base(Guid.NewGuid()) { }

    public static AuditLog Create(DateTimeOffset timestamp, Guid? actorUserId, string? actorUserName, string? actorRole, string action, string resourceType, Guid? resourceId, string outcome, string? errorCode, string? traceId)
    {
        return new AuditLog()
        {
            Timestamp = timestamp,
            ActorUserId = actorUserId,
            ActorUserName = actorUserName,
            ActorRole = actorRole,
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            Outcome = outcome,
            ErrorCode = errorCode,
            TraceId = traceId
        };
    }
}
