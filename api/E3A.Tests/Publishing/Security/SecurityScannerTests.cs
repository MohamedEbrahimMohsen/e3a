using System.Text;
using E3A.Application.Options;
using E3A.Application.Publishing.Security;
using E3A.Application.Publishing.Shared;
using E3A.Tests.Publishing.Shared;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Publishing.Security;

public sealed class SecurityScannerTests
{
    private const string BlockLine = "rm -rf /";
    private const string WarnLine = "cat ~/.aws/credentials";
    private readonly PublishingOptions _options = PublishingOptionsFactory.Default();

    [Fact]
    public void Scan_ShouldReturnCleanReport_WhenTreeIsBenign()
    {
        var report = Scan(PluginFileFactory.Files("agents/reviewer.md", "skills/demo/SKILL.md"));

        report.Findings.Should().BeEmpty();
        report.IsBlocked.Should().BeFalse();
        report.HasWarnings.Should().BeFalse();
        report.IsTruncated.Should().BeFalse();
        report.ScannedFileCount.Should().Be(2);
    }

    [Fact]
    public void Scan_ShouldReturnEmptyReport_WhenTreeIsEmpty()
    {
        var report = Scan([]);

        report.Findings.Should().BeEmpty();
        report.HookScriptCount.Should().Be(0);
        report.ScannedFileCount.Should().Be(0);
    }

    [Fact]
    public void Scan_ShouldCountHookScripts_WhenTreeContainsScriptExtensions()
    {
        var report = Scan(PluginFileFactory.Files("hooks/a.sh", "hooks/b.ps1", "hooks/c.py", "hooks/d.js", "agents/e.md", "agents/f.md"));

        report.HookScriptCount.Should().Be(4);
    }

    [Fact]
    public void Scan_ShouldNotCountHookScripts_WhenTreeIsMarkdownOnly()
    {
        Scan(PluginFileFactory.Files("agents/e.md", "agents/f.md")).HookScriptCount.Should().Be(0);
    }

    [Fact]
    public void Scan_ShouldSkipPatternRules_WhenFileIsBinary()
    {
        var report = Scan(ScanCorpusFactory.Binary([0x00, .. Encoding.UTF8.GetBytes(BlockLine)]));

        report.Findings.Should().NotContain(x => x.RuleId == ScanRuleIds.RecursiveRootDeletion);
        report.ScannedFileCount.Should().Be(0);
    }

    [Fact]
    public void Scan_ShouldSkipPatternRules_WhenFileIsNotValidUtf8()
    {
        var report = Scan(ScanCorpusFactory.Binary([0xC3, 0x28, .. Encoding.UTF8.GetBytes(BlockLine)]));

        report.Findings.Should().NotContain(x => x.RuleId == ScanRuleIds.RecursiveRootDeletion);
        report.ScannedFileCount.Should().Be(0);
    }

    [Fact]
    public void Scan_ShouldStillApplyHygieneRules_WhenFileIsBinary()
    {
        var report = SecurityScanner.Scan(ScanCorpusFactory.Binary(new byte[65]), ScanCorpusFactory.ScriptExtensions, PublishingOptionsFactory.Default(maxPluginFileBytes: 64));

        report.ScannedFileCount.Should().Be(0);
        report.Findings.Should().ContainSingle().Which.RuleId.Should().Be(ScanRuleIds.FileOverSizeCap);
    }

    [Fact]
    public void Scan_ShouldReportCorrectLineNumber_WhenMatchIsOnThirdLine()
    {
        var report = Scan(ScanCorpusFactory.Markdown($"first line\nsecond line\n  {BlockLine}  \nfourth line"));

        var finding = report.Findings.Should().ContainSingle().Subject;
        finding.Line.Should().Be(3);
        finding.Excerpt.Should().Be(BlockLine);
    }

    [Fact]
    public void Scan_ShouldTruncateExcerpt_WhenLineExceedsExcerptMaxLength()
    {
        var report = Scan(ScanCorpusFactory.Markdown($"{BlockLine} {new string('x', 300)}"));

        report.Findings.Should().ContainSingle().Which.Excerpt.Length.Should().Be(_options.ScanExcerptMaxLength);
    }

    [Fact]
    public void Scan_ShouldEmitOneFindingPerRulePerLine_WhenRuleMatchesTwiceOnOneLine()
    {
        var report = Scan(ScanCorpusFactory.Markdown($"{BlockLine} and {BlockLine}"));

        report.Findings.Should().ContainSingle(x => x.RuleId == ScanRuleIds.RecursiveRootDeletion && x.Line == 1 && x.Path == ScanCorpusFactory.MarkdownPath);
    }

    [Fact]
    public void Scan_ShouldOrderFindings_ByBlockSeverityThenPathThenLine()
    {
        var report = Scan([Markdown("a.md", WarnLine), Markdown("b.md", $"{BlockLine}\n{WarnLine}")]);

        report.Findings.Select(x => (x.Path, x.Line, x.Severity)).Should().Equal(("b.md", 1, ScanSeverity.Block), ("a.md", 1, ScanSeverity.Warn), ("b.md", 2, ScanSeverity.Warn));
    }

    [Fact]
    public void Scan_ShouldProduceIdenticalReport_WhenFileOrderIsShuffled()
    {
        List<PluginFile> files = [Markdown("c.md", WarnLine), Markdown("a.md", BlockLine), Markdown("b.md", "cat ~/.npmrc")];

        var report = Scan(files);
        var shuffled = Scan([.. Enumerable.Reverse(files)]);

        report.Findings.Should().HaveCount(3);
        shuffled.Findings.Should().Equal(report.Findings);
    }

    [Fact]
    public void Scan_ShouldTruncateFindings_WhenCountExceedsMaxScanFindings()
    {
        var report = SecurityScanner.Scan(ScanCorpusFactory.Markdown(string.Join('\n', Enumerable.Repeat(BlockLine, 5))), ScanCorpusFactory.ScriptExtensions, PublishingOptionsFactory.Default(maxScanFindings: 3));

        report.Findings.Should().HaveCount(3);
        report.IsTruncated.Should().BeTrue();
    }

    [Fact]
    public void Scan_ShouldKeepBlockFindings_WhenReportIsTruncated()
    {
        var content = string.Join('\n', [.. Enumerable.Repeat(WarnLine, 4), BlockLine]);

        var report = SecurityScanner.Scan(ScanCorpusFactory.Markdown(content), ScanCorpusFactory.ScriptExtensions, PublishingOptionsFactory.Default(maxScanFindings: 1));

        report.Findings.Should().ContainSingle().Which.RuleId.Should().Be(ScanRuleIds.RecursiveRootDeletion);
        report.IsBlocked.Should().BeTrue();
        report.IsTruncated.Should().BeTrue();
    }

    [Fact]
    public void Scan_ShouldTreatCrLfAndLfIdentically_WhenCountingLines()
    {
        var lineFeed = Scan(ScanCorpusFactory.Markdown($"alpha\n{BlockLine}\nbeta"));
        var carriageReturn = Scan(ScanCorpusFactory.Markdown($"alpha\r\n{BlockLine}\r\nbeta"));

        carriageReturn.Findings.Should().Equal(lineFeed.Findings);
        lineFeed.Findings.Should().ContainSingle().Which.Line.Should().Be(2);
    }

    private static PluginFile Markdown(string path, string content)
    {
        return new PluginFile(path, Encoding.UTF8.GetBytes(content));
    }

    private ScanReport Scan(List<PluginFile> files)
    {
        return SecurityScanner.Scan(files, ScanCorpusFactory.ScriptExtensions, _options);
    }
}
