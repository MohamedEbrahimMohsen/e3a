namespace Core.Auditing;

public sealed class AuditOptions
{
    public const string SectionName = "CoreAuditing";
    public bool Enabled { get; set; }
}
