using E3A.Application.Options;
using E3A.Domain.Publishing;
using E3A.Domain.Reports;

namespace E3A.Tests.Reports.Shared;

public static class ReportFactory
{
    public const string DefaultDetails = "It exfiltrates credentials.";

    public static Report Anonymous(Guid itemId, ItemType itemType = ItemType.Engineer, ReportReason reason = ReportReason.Malicious, string? details = DefaultDetails)
    {
        return Report.Create(itemType, itemId, null, reason, details);
    }

    public static Report Attributed(Guid itemId, Guid reporterUserId, ItemType itemType = ItemType.Engineer, ReportReason reason = ReportReason.Malicious, string? details = DefaultDetails)
    {
        return Report.Create(itemType, itemId, reporterUserId, reason, details);
    }

    public static ReportsOptions CreateReportsOptions(int maxReportsPerItem = 20, int detailsMaxLength = 1000)
    {
        return new ReportsOptions
        {
            MaxReportsPerItem = maxReportsPerItem,
            DetailsMaxLength = detailsMaxLength,
        };
    }
}
