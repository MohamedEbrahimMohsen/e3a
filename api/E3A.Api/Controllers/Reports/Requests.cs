using System.Text.Json.Serialization;
using E3A.Domain.Publishing;
using E3A.Domain.Reports;

namespace E3A.Api.Controllers.Reports;

public sealed record SubmitReportRequest([property: JsonRequired] ItemType ItemType, [property: JsonRequired] Guid ItemId, [property: JsonRequired] ReportReason Reason, string? Details);
