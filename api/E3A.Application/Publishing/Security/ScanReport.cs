namespace E3A.Application.Publishing.Security;

public sealed record ScanReport(List<ScanFinding> Findings, int HookScriptCount, int ScannedFileCount, bool IsTruncated)
{
    public bool IsBlocked => Findings.Exists(x => x.Severity == ScanSeverity.Block);
    public bool HasWarnings => Findings.Exists(x => x.Severity == ScanSeverity.Warn);
}
