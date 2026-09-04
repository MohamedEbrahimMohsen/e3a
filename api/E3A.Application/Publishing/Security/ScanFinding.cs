namespace E3A.Application.Publishing.Security;

public sealed record ScanFinding(string RuleId, string Category, ScanSeverity Severity, string Path, int Line, string Excerpt);
