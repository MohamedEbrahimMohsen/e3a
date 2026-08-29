using E3A.Application.Exceptions;
using E3A.Application.Options;
using E3A.Application.Publishing.Security;
using E3A.Application.Publishing.Shared;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Publishing.Shared;

public sealed class PublishStatusResultGeneratorTests
{
    private readonly PublishingOptions _options = PublishingOptionsFactory.Default();
    private readonly Guid _engineerId = Guid.NewGuid();

    [Fact]
    public void Generate_ShouldBuildAbsoluteZipUrl_WhenVersionIsPublished()
    {
        var version = ItemVersionFactory.Published(_engineerId);

        var result = PublishStatusResultGenerator.Generate(version, _options);

        result.ZipUrl.Should().Be("https://e3a.dev/z/e3a-dive-backend-engineer/1.0.0.zip");
        result.Status.Should().Be("Published");
        result.UpdatedAt.Should().Be(version.UpdationDate);
        result.EngineerId.Should().Be(_engineerId);
    }

    [Fact]
    public void Generate_ShouldExposeScanReport_WhenVersionHasScanReportJson()
    {
        var scanReportJson = ScanReportSerializer.Serialize(new ScanReport([new ScanFinding(ScanRuleIds.RecursiveRootDeletion, ScanCategories.DangerousCommand, ScanSeverity.Block, "hooks/hook.sh", 3, "rm -rf /")], 1, 2, false), _options);
        var version = ItemVersionFactory.Rejected(_engineerId, ErrorCodes.PluginSecurityScanBlocked, scanReportJson);

        var result = PublishStatusResultGenerator.Generate(version, _options);

        result.ScanReport.Should().NotBeNull();
        result.ScanReport!.IsBlocked.Should().BeTrue();
        result.ScanReport.Findings[0].RuleId.Should().Be(ScanRuleIds.RecursiveRootDeletion);
    }

    [Fact]
    public void Generate_ShouldReturnNullScanReport_WhenVersionHasNoScanReportJson()
    {
        PublishStatusResultGenerator.Generate(ItemVersionFactory.Queued(_engineerId), _options).ScanReport.Should().BeNull();
    }

    [Fact]
    public void Generate_ShouldReturnNullZipUrl_WhenVersionHasNoZip()
    {
        var version = ItemVersionFactory.Queued(_engineerId);

        var result = PublishStatusResultGenerator.Generate(version, _options);

        result.ZipUrl.Should().BeNull();
        result.Status.Should().Be("Queued");
    }
}
