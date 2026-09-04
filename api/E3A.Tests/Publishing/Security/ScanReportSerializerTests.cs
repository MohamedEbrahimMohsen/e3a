using E3A.Application.Publishing.Security;
using E3A.Tests.Publishing.Shared;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Publishing.Security;

public sealed class ScanReportSerializerTests
{
    private static readonly ScanReport Report = new([Finding(ScanRuleIds.RecursiveRootDeletion, ScanSeverity.Block, 1), Finding(ScanRuleIds.CredentialPathReference, ScanSeverity.Warn, 2)], 1, 2, false);

    [Fact]
    public void Serialize_ShouldRoundTrip_WhenReportHasFindings()
    {
        var restored = ScanReportSerializer.Deserialize(ScanReportSerializer.Serialize(Report, PublishingOptionsFactory.Default()));

        restored.Should().NotBeNull();
        restored!.Findings.Should().Equal(Report.Findings);
        restored.HookScriptCount.Should().Be(Report.HookScriptCount);
        restored.ScannedFileCount.Should().Be(Report.ScannedFileCount);
        restored.IsTruncated.Should().Be(Report.IsTruncated);
    }

    [Fact]
    public void Serialize_ShouldWriteSeverityAsString_WhenReportHasFindings()
    {
        var json = ScanReportSerializer.Serialize(Report, PublishingOptionsFactory.Default());

        json.Should().Contain("\"severity\":\"Block\"").And.Contain("\"ruleId\"").And.Contain("\"hookScriptCount\"");
    }

    [Fact]
    public void Serialize_ShouldRespectJsonLengthCap_WhenExcerptsAreLarge()
    {
        var json = ScanReportSerializer.Serialize(Bloated(), PublishingOptionsFactory.Default(scanReportJsonMaxLength: 400));

        json.Length.Should().BeLessThanOrEqualTo(400);
    }

    [Fact]
    public void Serialize_ShouldSetTruncatedFlag_WhenFindingsAreDropped()
    {
        var bloated = Bloated();

        var restored = ScanReportSerializer.Deserialize(ScanReportSerializer.Serialize(bloated, PublishingOptionsFactory.Default(scanReportJsonMaxLength: 400)));

        restored.Should().NotBeNull();
        restored!.IsTruncated.Should().BeTrue();
        restored.Findings.Count.Should().BeLessThan(bloated.Findings.Count);
    }

    [Fact]
    public void Serialize_ShouldNotTruncate_WhenReportFitsUnderCap()
    {
        var restored = ScanReportSerializer.Deserialize(ScanReportSerializer.Serialize(Report, PublishingOptionsFactory.Default()));

        restored.Should().NotBeNull();
        restored!.IsTruncated.Should().BeFalse();
        restored.Findings.Should().HaveCount(Report.Findings.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Deserialize_ShouldReturnNull_WhenJsonIsNullOrWhitespace(string? scanReportJson)
    {
        ScanReportSerializer.Deserialize(scanReportJson).Should().BeNull();
    }

    private static ScanReport Bloated()
    {
        return new ScanReport([.. Enumerable.Range(1, 20).Select(line => Finding(ScanRuleIds.RecursiveRootDeletion, ScanSeverity.Block, line))], 0, 1, false);
    }

    private static ScanFinding Finding(string ruleId, ScanSeverity severity, int line)
    {
        return new ScanFinding(ruleId, ScanCategories.DangerousCommand, severity, "skills/demo/SKILL.md", line, new string('x', 200));
    }
}
