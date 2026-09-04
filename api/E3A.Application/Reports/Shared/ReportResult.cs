namespace E3A.Application.Reports.Shared;

public sealed record ReportResult(Guid Id, string Status, DateTimeOffset CreatedAt);
