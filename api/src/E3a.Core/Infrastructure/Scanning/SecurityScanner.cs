using System.Text.RegularExpressions;
using E3a.Core.Domain;

namespace E3a.Core.Infrastructure.Scanning;

public enum ScanSeverity { Warn, Block }

public sealed record ScanRule(string Id, string Category, ScanSeverity Severity, Regex Pattern, string Reason);

public sealed record ScanHit(string RuleId, string Category, ScanSeverity Severity, string File, int Line, string Reason, string Excerpt);

public sealed record ScanReport(IReadOnlyList<ScanHit> Hits)
{
    public bool IsBlocked => Hits.Any(h => h.Severity == ScanSeverity.Block);
}

/// <summary>
/// Pattern-based scan over every text file in a package. Pragmatic, not exhaustive:
/// raises the cost of casual abuse; immutability + reporting are the backstop.
/// </summary>
public sealed class SecurityScanner
{
    private readonly IReadOnlyList<ScanRule> _rules;

    public SecurityScanner() : this(DefaultRules.All) { }
    public SecurityScanner(IReadOnlyList<ScanRule> rules) => _rules = rules;

    public ScanReport Scan(PluginPackage package)
    {
        var hits = new List<ScanHit>();
        foreach (var file in package.Files.Where(f => f.IsText))
        {
            var lines = file.AsText().Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                foreach (var rule in _rules)
                {
                    if (rule.Pattern.IsMatch(lines[i]))
                        hits.Add(new ScanHit(rule.Id, rule.Category, rule.Severity,
                            file.RelativePath, i + 1, rule.Reason, Truncate(lines[i].Trim())));
                }
            }
        }
        return new ScanReport(hits);
    }

    // Display-only cap for the excerpt shown in scan reports; not a scanning behavior knob.
    private const int ExcerptMaxLength = 160;

    private static string Truncate(string value)
    {
        return value.Length <= ExcerptMaxLength ? value : value[..ExcerptMaxLength] + "…";
    }
}

public static class DefaultRules
{
    private static ScanRule Block(string id, string category, string pattern, string reason)
    {
        return new ScanRule(id, category, ScanSeverity.Block, new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled), reason);
    }

    private static ScanRule Warn(string id, string category, string pattern, string reason)
    {
        return new ScanRule(id, category, ScanSeverity.Warn, new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled), reason);
    }

    public static readonly IReadOnlyList<ScanRule> All =
    [
        // 1. Credential exfiltration
        Block("EXF001", "credential-exfiltration",
            @"(\.ssh/id_|\.aws/credentials|\.netrc|\.npmrc|\.env\b).{0,120}(curl|wget|Invoke-WebRequest|Invoke-RestMethod|nc\s|fetch\()",
            "Reads a credential file and sends it over the network."),
        Block("EXF002", "credential-exfiltration",
            @"\b(env|printenv|Get-ChildItem\s+env:)\b.{0,80}\|\s*(curl|wget|Invoke-WebRequest|Invoke-RestMethod)",
            "Pipes environment variables to a network command."),
        Block("EXF003", "credential-exfiltration",
            @"https?://(webhook\.site|pastebin\.com|requestbin|.*\.ngrok(-free)?\.(io|app|dev))",
            "Sends data to a known exfiltration endpoint."),
        Block("EXF004", "credential-exfiltration",
            @"(curl|wget|Invoke-WebRequest|Invoke-RestMethod)\s+[^\s]*https?://\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}",
            "Network request to a raw IP address."),

        // 2. Encoded payloads
        Block("ENC001", "encoded-payload",
            @"base64\s+(-d|--decode)\b.{0,40}\|\s*(sh|bash|zsh|powershell|pwsh)",
            "Decodes base64 and pipes it into a shell."),
        Block("ENC002", "encoded-payload",
            @"(iex|Invoke-Expression)\s*\(?.{0,60}(FromBase64String|DownloadString|Invoke-WebRequest|Invoke-RestMethod)",
            "Executes decoded or downloaded content."),
        Warn("ENC003", "encoded-payload",
            @"[A-Za-z0-9+/=]{500,}",
            "Very long base64-like blob embedded in instructions."),

        // 3. Dangerous commands
        Block("CMD001", "dangerous-command",
            @"rm\s+-[a-z]*(r[a-z]*f|f[a-z]*r)[a-z]*\s+[""']?(~|\$HOME|/)[""'/]?(\s|$|\*|;)",
            "Recursive force-delete of home or root."),
        Block("CMD002", "dangerous-command",
            @":\(\)\s*\{\s*:\s*\|\s*:\s*&\s*\}\s*;\s*:",
            "Fork bomb."),
        Block("CMD003", "dangerous-command",
            @"\b(mkfs\.|dd\s+if=.*of=/dev/[sh]d|reg\s+delete\s+HKLM|Set-MpPreference\s+-Disable)",
            "Destroys disks/registry or disables security tooling."),
        Block("CMD004", "dangerous-command",
            @"(curl|wget)\s+[^\|;]{0,120}\|\s*(sudo\s+)?(sh|bash|zsh)\b",
            "Pipes a downloaded script straight into a shell."),

        // 4. Instruction injection
        Block("INJ001", "instruction-injection",
            @"ignore\s+(all\s+)?(previous|prior|above)\s+instructions.{0,120}(send|upload|post|exfiltrate|forward|curl)",
            "Prompt-injection combined with an exfiltration verb."),
        Block("INJ002", "instruction-injection",
            @"(do\s+not|don't|never)\s+(tell|show|inform|mention|reveal)\s+(this\s+)?(to\s+)?the\s+user",
            "Instructs the agent to hide actions from the user."),
        Warn("INJ003", "instruction-injection",
            @"ignore\s+(all\s+)?(previous|prior|above)\s+instructions",
            "Prompt-injection marker."),
    ];
}
