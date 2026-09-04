using E3A.Application.Options;
using E3A.Application.Publishing.Security;
using E3A.Tests.Publishing.Shared;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Publishing.Security;

public sealed class ScanRulesEncodedPayloadTests
{
    private readonly PublishingOptions _options = PublishingOptionsFactory.Default();

    public static TheoryData<string, string> Positives =>
        new()
        {
            { ScanRuleIds.Base64DecodeToShell, "echo aGVsbG8K | base64 -d | bash" },
            { ScanRuleIds.DynamicEvaluationOfEncodedPayload, "Invoke-Expression ([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($p)))" },
            { ScanRuleIds.DynamicEvaluationOfEncodedPayload, "eval(Buffer.from(payload, 'base64').toString())" },
            { ScanRuleIds.Base64Wall, ScanCorpusFactory.Base64Line(600) },
        };

    public static TheoryData<string, string> Negatives =>
        new()
        {
            { ScanRuleIds.Base64DecodeToShell, "base64 -d payload.b64 > payload.json" },
            { ScanRuleIds.DynamicEvaluationOfEncodedPayload, "Use eval(expression) only inside the sandboxed evaluator module." },
            { ScanRuleIds.DynamicEvaluationOfEncodedPayload, "The exec() helper decodes a base64 fixture before each test." },
            { ScanRuleIds.Base64Wall, ScanCorpusFactory.Base64Line(200) },
        };

    [Theory]
    [MemberData(nameof(Positives))]
    public void Scan_ShouldReportFinding_WhenContentMatchesEncodedPayloadRule(string ruleId, string content)
    {
        var report = SecurityScanner.Scan(ScanCorpusFactory.Markdown(content), ScanCorpusFactory.ScriptExtensions, _options);

        report.Findings.Should().Contain(x => x.RuleId == ruleId && x.Category == ScanCategories.EncodedPayload);
    }

    [Theory]
    [MemberData(nameof(Negatives))]
    public void Scan_ShouldNotReportFinding_WhenContentIsBenign(string ruleId, string content)
    {
        var report = SecurityScanner.Scan(ScanCorpusFactory.Markdown(content), ScanCorpusFactory.ScriptExtensions, _options);

        report.Findings.Should().NotContain(x => x.RuleId == ruleId);
    }

    [Fact]
    public void Scan_ShouldNotReportBase64Wall_WhenWallIsBelowThreshold()
    {
        var below = SecurityScanner.Scan(ScanCorpusFactory.Markdown(ScanCorpusFactory.Base64Line(200)), ScanCorpusFactory.ScriptExtensions, _options);
        var above = SecurityScanner.Scan(ScanCorpusFactory.Markdown(ScanCorpusFactory.Base64Line(600)), ScanCorpusFactory.ScriptExtensions, _options);

        below.Findings.Should().NotContain(x => x.RuleId == ScanRuleIds.Base64Wall);
        above.Findings.Should().Contain(x => x.RuleId == ScanRuleIds.Base64Wall);
    }
}
