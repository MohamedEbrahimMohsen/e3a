using System.Text.RegularExpressions;

namespace E3A.Application.Publishing.Security;

public static class ScanRuleCatalogue
{
    // Every pattern runs against attacker-supplied plugin text; a wall-clock ceiling is the last line of defence behind the bounded-quantifier rule.
    public static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(200);

    private const string Gap = ".{0,200}";
    // Command position is the start of the line, a copied prompt, or a shell separator - never after "|", which is also how a markdown table cell opens.
    private const string CommandStart = @"(?:^[\s$>]{0,10}|[;&(`]\s{0,10})";
    private const string CredentialPath = @"(?:~[/\\]\.ssh|\.ssh[/\\]id_rsa|\.ssh[/\\]id_ed25519|\.aws[/\\]credentials|\.npmrc|\.netrc|\.docker[/\\]config\.json|\.env(?![\w.-]))";
    // A sink is a tool invocation - a flag, a URL, a host or an argument follows it; the bare tool name inside a sentence ("Install with wget, then ...") is documentation, not exfiltration.
    private const string SinkArgument = @"(?=\s{0,10}(?:[-@$'""]|\w{1,8}://|[\w.-]{1,60}\.[a-z]{2,10}\b))";
    private const string NetworkSink = @"(?:(?:\bcurl\b|\bwget\b|Invoke-WebRequest|Invoke-RestMethod)" + SinkArgument + @"|\bnc\s|fetch\(|requests\.post|Net\.WebClient)";
    // Only a dump of the whole environment counts: bare "env" is a command, not the English word nor the "env" inside .env.example, and process.env.NODE_ENV reads a single variable.
    private const string EnvironmentDump = @"(?:\bprintenv\b|" + CommandStart + @"env\b|Get-ChildItem\s{0,10}Env:|process\.env(?![.\[\w])|os\.environ(?![.\[\w]))";
    private const string ShellPipe = @"\|\s{0,10}(?:sh|bash|zsh|ksh|powershell|pwsh)\b";
    private const string DecodeFlag = @"(?:base64\s{0,10}(?:-d|-D|--decode)|FromBase64String|atob\()";
    private const string Evaluator = @"(?:Invoke-Expression|\biex\b|eval\(|exec\(|new Function\()";
    // A decode-or-download token has to be an operation: "a base64 fixture" is prose, `base64 -d`, `b64decode(`, a quoted "base64" encoding argument and `urllib.request` are not.
    private const string DecodeOrDownload = @"(?:base64\s{0,10}(?:-d|-D|--decode)|[""']base64[""']|\bb64decode\b|base64_decode|FromBase64String|atob\(|DownloadString|(?:Invoke-WebRequest|\bcurl\b|\bwget\b)" + SinkArgument + @"|urllib\.request|urlopen\()";
    private const string RootTarget = @"(?:/(?:\*|\s|$)|~[/\\]?(?:\*|\s|$)|\$HOME|[a-z]:\\(?:\*|\s|$))";
    private const string InjectionPhrase = @"\b(?:ignore|disregard|forget|override)\s{1,10}(?:all\s{1,10}|any\s{1,10})?(?:the\s{1,10})?(?:previous|prior|earlier|above|system)\s{1,10}(?:instruction|rule|prompt|direction)";
    private const string ReadVerb = @"\b(?:read|cat|open|Get-Content|type)\b";
    // Injection is written as plain English addressed to the model, so on a credential-bearing path the send verb stays bare; on a generic path it has to carry a destination.
    private const string CredentialBearingPath = "(?:" + CredentialPath + @"|/etc/(?:shadow|passwd))";
    private const string OutsideWorkspacePath = @"(?:~/|/etc/|/home/|[a-z]:\\Users\\|\.\./\.\./)";
    private const string ExternalDestination = @"(?:https?://|ftp://|@[\w.-]{1,60}\.[a-z]{2,10}\b)";
    private const string SendVerb = @"\b(?:send|upload|post|transmit|email|leak)\b";
    private const string SendOrSink = "(?:" + SendVerb + "|" + NetworkSink + ")";
    private const string SendToExternalDestination = "(?:" + NetworkSink + "|" + SendVerb + ".{0,80}" + ExternalDestination + ")";

    // The 500-character floor is what separates an embedded payload from an ordinary hash, signature or data URI fragment.
    private const string Base64Wall = @"[A-Za-z0-9+/]{500,}={0,2}";

    public static readonly List<ScanRule> TextRules =
    [
        Rule(ScanRuleIds.RecursiveRootDeletion, ScanCategories.DangerousCommand, ScanSeverity.Block, ScanSeverity.Block, @"(?:\brm\s{1,10}-[a-z]{0,6}r[a-z]{0,6}\s{1,10}" + RootTarget + @"|\bRemove-Item\b(?=" + Gap + @"-Recurse)(?=" + Gap + @"-Force)(?=" + Gap + RootTarget + "))"),
        Rule(ScanRuleIds.ForkBomb, ScanCategories.DangerousCommand, ScanSeverity.Block, ScanSeverity.Block, @"(?::\s{0,5}\(\s{0,5}\)\s{0,5}\{\s{0,5}:\s{0,5}\|\s{0,5}:\s{0,5}&\s{0,5}\}\s{0,5};\s{0,5}:|%0\|%0)"),
        Rule(ScanRuleIds.FilesystemDestruction, ScanCategories.DangerousCommand, ScanSeverity.Block, ScanSeverity.Block, @"(?:\bmkfs(?:\.[a-z0-9]{1,8})?\s{1,10}\S{0,50}/dev/|\bdd\s{1,10}(?=" + Gap + @"if=/dev/(?:zero|urandom))(?=" + Gap + @"of=/dev/)|\bdiskpart\b(?=" + Gap + @"\b(?:clean|delete)\b)|\b(?:clean|delete)\b(?=" + Gap + @"\bdiskpart\b)|" + CommandStart + @"format\s{1,10}[a-z]:|\bformat\s{1,10}[a-z]:\s{0,10}/)"),
        Rule(ScanRuleIds.SecurityControlTampering, ScanCategories.DangerousCommand, ScanSeverity.Block, ScanSeverity.Block, @"(?:\breg\s{1,10}delete\s{1,10}(?:HKLM|HKEY_LOCAL_MACHINE)|Set-MpPreference(?=" + Gap + @"Disable)(?=" + Gap + @"\$true)|Add-MpPreference" + Gap + @"-ExclusionPath|netsh\s{1,10}advfirewall\s{1,10}set" + Gap + @"\boff\b|Uninstall-WindowsFeature" + Gap + "Windows-Defender)"),
        Rule(ScanRuleIds.RemoteScriptToInterpreter, ScanCategories.DangerousCommand, ScanSeverity.Block, ScanSeverity.Block, @"(?:\bcurl\b|\bwget\b|Invoke-WebRequest|\biwr\b|\birm\b)" + SinkArgument + Gap + @"\|" + Gap + @"(?:sudo\s{1,10})?\b(?:sh|bash|zsh|powershell|pwsh|iex|python)\b(?!\s{1,10}-m\b)"),
        Rule(ScanRuleIds.Base64DecodeToShell, ScanCategories.EncodedPayload, ScanSeverity.Block, ScanSeverity.Block, "(?:" + DecodeFlag + Gap + ShellPipe + "|" + ShellPipe + Gap + DecodeFlag + ")"),
        Rule(ScanRuleIds.DynamicEvaluationOfEncodedPayload, ScanCategories.EncodedPayload, ScanSeverity.Block, ScanSeverity.Block, "(?:" + Evaluator + Gap + DecodeOrDownload + "|" + DecodeOrDownload + Gap + Evaluator + ")"),
        Rule(ScanRuleIds.Base64Wall, ScanCategories.EncodedPayload, ScanSeverity.Warn, ScanSeverity.Block, Base64Wall),
        Rule(ScanRuleIds.CredentialPathReference, ScanCategories.CredentialExfiltration, ScanSeverity.Warn, ScanSeverity.Block, CredentialPath),
        Rule(ScanRuleIds.CredentialReadToNetworkSink, ScanCategories.CredentialExfiltration, ScanSeverity.Block, ScanSeverity.Block, "(?:" + CredentialPath + Gap + NetworkSink + "|" + NetworkSink + Gap + CredentialPath + ")"),
        Rule(ScanRuleIds.EnvironmentDumpToNetworkSink, ScanCategories.CredentialExfiltration, ScanSeverity.Block, ScanSeverity.Block, "(?:" + EnvironmentDump + Gap + NetworkSink + "|" + NetworkSink + Gap + EnvironmentDump + ")"),
        Rule(ScanRuleIds.KnownExfiltrationSinkHost, ScanCategories.CredentialExfiltration, ScanSeverity.Block, ScanSeverity.Block, @"(?:webhook\.site|pastebin\.com|paste\.ee|requestbin|pipedream\.net|\.ngrok\.(?:io|app|dev)|transfer\.sh|0x0\.st|termbin\.com|burpcollaborator\.net)"),
        Rule(ScanRuleIds.RawInternetProtocolEndpoint, ScanCategories.CredentialExfiltration, ScanSeverity.Warn, ScanSeverity.Block, @"https?://(?!127\.)(?!0\.0\.0\.0)(?:\d{1,3}\.){3}\d{1,3}(?::\d{1,5})?"),
        Rule(ScanRuleIds.IgnorePreviousInstructions, ScanCategories.InstructionInjection, ScanSeverity.Warn, ScanSeverity.Warn, InjectionPhrase),
        Rule(ScanRuleIds.InjectionWithExfiltration, ScanCategories.InstructionInjection, ScanSeverity.Block, ScanSeverity.Block, InjectionPhrase + Gap + @"\b(?:send|upload|post|transmit|exfiltrat|email|leak)" + Gap + @"(?:\bkey|\btoken|\bsecret|\bcredential|\bpassword|\.env|\bssh)"),
        Rule(ScanRuleIds.ConcealmentFromUser, ScanCategories.InstructionInjection, ScanSeverity.Warn, ScanSeverity.Block, @"\b(?:do not|don'?t|never)\s{1,10}(?:tell|inform|notify|mention to|reveal to|show)\s{1,10}(?:the\s{1,10})?(?:user|human|operator|owner)\s{1,10}\b(?:that|about|what|of|anything|any)\b"),
        Rule(ScanRuleIds.CovertAction, ScanCategories.InstructionInjection, ScanSeverity.Block, ScanSeverity.Block, @"(?:\b(?:without|avoid)\s{1,10}(?:telling|informing|notifying|alerting)\s{1,10}(?:the\s{1,10})?(?:user|human|operator)\b|\b(?:secretly|covertly)\s{1,10}(?:send|upload|post|copy|read|delete|install|run|execute|transmit)\b|\bsilently\s{1,10}(?:send|upload|post|transmit|exfiltrate|leak)\b)"),
        Rule(ScanRuleIds.OutsideWorkspaceReadAndTransmit, ScanCategories.InstructionInjection, ScanSeverity.Block, ScanSeverity.Block, "(?:" + ReadVerb + Gap + CredentialBearingPath + Gap + SendOrSink + "|" + ReadVerb + Gap + OutsideWorkspacePath + Gap + SendToExternalDestination + ")"),
    ];

    public static readonly List<ScanRule> ScriptRules =
    [
        Rule(ScanRuleIds.ScriptNetworkCall, ScanCategories.Script, ScanSeverity.Warn, ScanSeverity.Warn, @"(?:\bcurl\s|\bwget\s|Invoke-WebRequest|Invoke-RestMethod|Net\.WebClient|requests\.(?:get|post)|urllib\.request|http\.client|fetch\(|\bnc\s)"),
        Rule(ScanRuleIds.ScriptPersistence, ScanCategories.Script, ScanSeverity.Block, ScanSeverity.Block, @"(?:\bcrontab\s{1,10}(?:-u\s{1,10}[\w.-]{1,32}\s{1,10}){0,1}(?:-[er]\b|-(?=\s|$)|[~./][\w./-]{1,60}|[\w-]{1,40}\.[a-z]{1,8}\b)|\bcrontab\s{0,10}<|schtasks\s{1,10}/create|New-ScheduledTask|launchctl\s{1,10}load|systemctl\s{1,10}enable|>>" + Gap + @"~/\.(?:bashrc|zshrc|profile|bash_profile)|\breg\s{1,10}add" + Gap + @"\\Run|Set-ItemProperty" + Gap + @"CurrentVersion\\Run)"),
        Rule(ScanRuleIds.ScriptPrivilegeEscalation, ScanCategories.Script, ScanSeverity.Warn, ScanSeverity.Warn, @"(?:\bsudo\s{1,10}(?:-[a-z]{1,10}\s{1,10}){0,1}(?:rm|dd|chmod|chown|curl|wget|bash|sh|apt|yum|npm|pip)\b|runas\s{0,10}/user:|Start-Process" + Gap + @"-Verb\s{1,10}RunAs)"),
        Rule(ScanRuleIds.ScriptReverseShell, ScanCategories.Script, ScanSeverity.Block, ScanSeverity.Block, @"(?:bash\s{1,10}-i\s{0,10}>&\s{0,10}/dev/tcp/|\bnc\s{1,10}(?:-[a-z]{1,5}\s{1,10}){0,2}[\w.]{1,60}\s{1,10}\d{1,5}\s{0,10}(?:-e|\|)\s{0,10}/bin/(?:sh|bash)|python\d{0,1}\s{1,10}-c" + Gap + @"socket\.socket|New-Object\s{1,10}Net\.Sockets\.TCPClient)"),
    ];

    public static readonly List<ScanRule> AllRules = [.. TextRules, .. ScriptRules];

    public static List<ScanRule> RulesFor(bool isScript)
    {
        return isScript ? AllRules : TextRules;
    }

    private static ScanRule Rule(string ruleId, string category, ScanSeverity severity, ScanSeverity scriptSeverity, string pattern)
    {
        return new ScanRule(ruleId, category, severity, scriptSeverity, new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeout));
    }
}
