using System.Text.RegularExpressions;
using E3A.Application.Publishing.Security;
using E3A.Tests.Publishing.Shared;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Publishing.Security;

public sealed class SecurityScannerRedosTests
{
    public static TheoryData<string> AdversarialLines =>
        new()
        {
            new string('a', 50000),
            new string('/', 50000),
            new string('.', 50000),
            string.Concat(Enumerable.Repeat("curl ", 20000)),
            ScanCorpusFactory.Base64Line(50000) + "!",
        };

    [Theory]
    [MemberData(nameof(AdversarialLines))]
    public void Scan_ShouldComplete_WhenLineIsLongRepeatedFiller(string line)
    {
        ScanReport? report = null;

        var act = () => report = SecurityScanner.Scan(ScanCorpusFactory.Markdown(line), ScanCorpusFactory.ScriptExtensions, PublishingOptionsFactory.Default());

        act.Should().NotThrow<RegexMatchTimeoutException>();
        report.Should().NotBeNull();
    }

    public static TheoryData<string> TimeoutShapes =>
        new()
        {
            string.Concat(Enumerable.Repeat("cat ~/.ssh/id_rsa ", 12000)),
            string.Concat(Enumerable.Repeat("env|curl$x;wget$y;printenv|nc$z", 6500)),
            string.Concat(Enumerable.Repeat("cat/home/dev/id_rsa/curl/", 8000)),
            string.Concat(Enumerable.Repeat("open/home/post/", 2134)),
            string.Concat(Enumerable.Repeat("open/home/post/", 1600)),
            string.Concat(Enumerable.Repeat("cat/home/send/", 2286)),
            string.Concat(Enumerable.Repeat("read/home/leak/", 2134)),
        };

    [Theory]
    [MemberData(nameof(TimeoutShapes))]
    public void Scan_ShouldReportLineOverCapDeterministically_WhenLineWouldExceedTheMatchTimeout(string line)
    {
        var options = PublishingOptionsFactory.Default();

        var first = SecurityScanner.Scan(ScanCorpusFactory.Markdown(line), ScanCorpusFactory.ScriptExtensions, options);
        var second = SecurityScanner.Scan(ScanCorpusFactory.Markdown(line), ScanCorpusFactory.ScriptExtensions, options);

        first.Findings.Should().Equal(second.Findings);
        first.Findings.Should().ContainSingle().Which.RuleId.Should().Be(ScanRuleIds.LineOverLengthCap);
    }

    [Fact]
    public void Scan_ShouldComplete_WhenFileIsManyAdversarialLines()
    {
        var options = PublishingOptionsFactory.Default();
        var content = string.Join('\n', Enumerable.Repeat(ScanCorpusFactory.Base64Line(600), 2000));

        var report = SecurityScanner.Scan(ScanCorpusFactory.Markdown(content), ScanCorpusFactory.ScriptExtensions, options);

        report.Findings.Should().HaveCount(options.MaxScanFindings);
        report.IsTruncated.Should().BeTrue();
    }
}
