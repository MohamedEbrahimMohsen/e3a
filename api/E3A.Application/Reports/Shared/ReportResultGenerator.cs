using E3A.Domain.Reports;

namespace E3A.Application.Reports.Shared;

public static class ReportResultGenerator
{
    public static ReportResult Generate(Report report)
    {
        return new ReportResult(report.Id, report.Status.ToString(), report.CreationDate);
    }
}
