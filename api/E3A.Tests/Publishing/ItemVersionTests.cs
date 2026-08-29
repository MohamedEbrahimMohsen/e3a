using E3A.Application.Exceptions;
using E3A.Domain.Publishing;
using E3A.Tests.Publishing.Shared;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Publishing;

public sealed class ItemVersionTests
{
    private const string FrozenManifestJson = "{\"imported\":[]}";
    private const string ScanReportJson = "{\"findings\":[],\"hookScriptCount\":1}";
    private readonly Guid _engineerId = Guid.NewGuid();

    [Fact]
    public void Create_ShouldReturnQueuedVersion_WhenCalled()
    {
        var version = ItemVersion.Create(ItemType.Engineer, _engineerId, 3, "1.2.3", FrozenManifestJson, Guid.NewGuid());

        version.Status.Should().Be(ItemVersionStatus.Queued);
        version.VersionNumber.Should().Be(3);
        version.SemanticVersion.Should().Be("1.2.3");
        version.FrozenManifestJson.Should().Be(FrozenManifestJson);
        version.SizeBytes.Should().Be(0);
        version.Id.Should().NotBe(Guid.Empty);
        version.ZipBlobPath.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldRaisePublishRequestedDomainEvent_WhenCalled()
    {
        var version = ItemVersionFactory.Queued(_engineerId);

        version.GetDomainEvents().Should().ContainSingle()
            .Which.Should().BeOfType<PublishRequestedDomainEvent>()
            .Which.VersionId.Should().Be(version.Id);
    }

    [Fact]
    public void MarkBuilding_ShouldSetBuildingAndAdvanceUpdationDate_WhenCalled()
    {
        var version = ItemVersionFactory.Queued(_engineerId);
        var before = DateTimeOffset.UtcNow;

        version.MarkBuilding();

        version.Status.Should().Be(ItemVersionStatus.Building);
        version.UpdationDate.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void MarkPublished_ShouldRecordZipMetadataAndClearFailureReason_WhenCalled()
    {
        var version = ItemVersionFactory.Failed(_engineerId, ErrorCodes.PluginTooLarge);
        var before = DateTimeOffset.UtcNow;

        version.MarkPublished("z/e3a-slug/1.0.0.zip", ItemVersionFactory.DefaultZipSha256, 4096);

        version.Status.Should().Be(ItemVersionStatus.Published);
        version.ZipBlobPath.Should().Be("z/e3a-slug/1.0.0.zip");
        version.ZipSha256.Should().Be(ItemVersionFactory.DefaultZipSha256);
        version.SizeBytes.Should().Be(4096);
        version.FailureReason.Should().BeNull();
        version.UpdationDate.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void MarkFailed_ShouldSetFailedWithReason_WhenCalled()
    {
        var version = ItemVersionFactory.Building(_engineerId);
        var before = DateTimeOffset.UtcNow;

        version.MarkFailed(ErrorCodes.EngineerSnapshotEmpty);

        version.Status.Should().Be(ItemVersionStatus.Failed);
        version.FailureReason.Should().Be(ErrorCodes.EngineerSnapshotEmpty);
        version.UpdationDate.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void RecordScanReport_ShouldStoreJsonAndAdvanceUpdationDate_WhenCalled()
    {
        var version = ItemVersionFactory.Building(_engineerId);
        var before = DateTimeOffset.UtcNow;

        version.RecordScanReport(ScanReportJson);

        version.ScanReportJson.Should().Be(ScanReportJson);
        version.Status.Should().Be(ItemVersionStatus.Building);
        version.UpdationDate.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void MarkRejected_ShouldSetRejectedWithReason_WhenCalled()
    {
        var version = ItemVersionFactory.Building(_engineerId);
        var before = DateTimeOffset.UtcNow;

        version.MarkRejected(ErrorCodes.PluginSecurityScanBlocked);

        version.Status.Should().Be(ItemVersionStatus.Rejected);
        version.FailureReason.Should().Be(ErrorCodes.PluginSecurityScanBlocked);
        version.UpdationDate.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void MarkRejected_ShouldKeepScanReport_WhenReportWasRecorded()
    {
        var version = ItemVersionFactory.Rejected(_engineerId, ErrorCodes.PluginSecurityScanBlocked, ScanReportJson);

        version.ScanReportJson.Should().Be(ScanReportJson);
        version.Status.Should().Be(ItemVersionStatus.Rejected);
    }

    [Fact]
    public void MarkPublished_ShouldKeepScanReport_WhenWarningsWereRecorded()
    {
        var version = ItemVersionFactory.Building(_engineerId);
        version.RecordScanReport(ScanReportJson);

        version.MarkPublished(ItemVersionFactory.DefaultZipBlobPath, ItemVersionFactory.DefaultZipSha256, ItemVersionFactory.DefaultSizeBytes);

        version.ScanReportJson.Should().Be(ScanReportJson);
        version.FailureReason.Should().BeNull();
    }

    [Fact]
    public void IsTerminal_ShouldBeTrue_WhenStatusIsRejected()
    {
        ItemVersionFactory.Rejected(_engineerId, ErrorCodes.PluginSecurityScanBlocked, ScanReportJson).IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void IsTerminal_ShouldBeTrue_WhenStatusIsPublishedOrFailed()
    {
        ItemVersionFactory.Queued(_engineerId).IsTerminal.Should().BeFalse();
        ItemVersionFactory.Building(_engineerId).IsTerminal.Should().BeFalse();
        ItemVersionFactory.Published(_engineerId).IsTerminal.Should().BeTrue();
        ItemVersionFactory.Failed(_engineerId, ErrorCodes.PluginTooLarge).IsTerminal.Should().BeTrue();
    }
}
