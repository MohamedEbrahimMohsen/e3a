namespace Core.Auditing;
public interface IAuditableCommand
{
    string AuditAction { get; }
    string AuditResourceType { get; }
    Guid? AuditResourceId { get; }
}
