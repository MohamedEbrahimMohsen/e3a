using System.Text.RegularExpressions;

namespace E3A.Application.Publishing.Security;

public sealed record ScanRule(string RuleId, string Category, ScanSeverity Severity, ScanSeverity ScriptSeverity, Regex Pattern)
{
    public ScanSeverity SeverityFor(bool isScript)
    {
        return isScript ? ScriptSeverity : Severity;
    }
}
