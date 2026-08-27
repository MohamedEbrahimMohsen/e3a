namespace Core.Auditing;

public interface IAuditableResult
{
    Guid? AuditResourceId { get; }
}
