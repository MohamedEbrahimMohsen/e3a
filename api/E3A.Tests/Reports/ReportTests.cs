using E3A.Domain.Publishing;
using E3A.Domain.Reports;
using E3A.Tests.Reports.Shared;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Reports;

public sealed class ReportTests
{
    [Fact]
    public void Create_ShouldStartOpenWithStampedDates_WhenReportIsCreated()
    {
        var itemId = Guid.NewGuid();
        var before = DateTimeOffset.UtcNow;

        var report = ReportFactory.Anonymous(itemId, ItemType.Team, ReportReason.Spam, "It spams the catalog.");

        report.Status.Should().Be(ReportStatus.Open);
        report.Id.Should().NotBe(Guid.Empty);
        report.ItemType.Should().Be(ItemType.Team);
        report.ItemId.Should().Be(itemId);
        report.Reason.Should().Be(ReportReason.Spam);
        report.Details.Should().Be("It spams the catalog.");
        report.CreationDate.Should().BeOnOrAfter(before);
        report.UpdationDate.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void Create_ShouldRecordReporterAndCreatedBy_WhenReporterIsSignedIn()
    {
        var reporterUserId = Guid.NewGuid();

        var report = ReportFactory.Attributed(Guid.NewGuid(), reporterUserId);

        report.ReporterUserId.Should().Be(reporterUserId);
        report.CreatedBy.Should().Be(reporterUserId);
        report.IsAnonymous.Should().BeFalse();
    }

    [Fact]
    public void Create_ShouldLeaveReporterUnset_WhenReportIsAnonymous()
    {
        var report = ReportFactory.Anonymous(Guid.NewGuid());

        report.ReporterUserId.Should().BeNull();
        report.CreatedBy.Should().BeNull();
        report.IsAnonymous.Should().BeTrue();
    }
}
