using E3A.Application.Reports.Shared;
using E3A.Domain.Publishing;
using E3A.Domain.Reports;
using MediatR;

namespace E3A.Application.Reports.SubmitReport;

public sealed record SubmitReportCommand(ItemType ItemType, Guid ItemId, ReportReason Reason, string? Details) : IRequest<ReportResult>;
