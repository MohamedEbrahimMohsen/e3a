using Core.Validation.Extensions;
using E3A.Application.Exceptions;
using E3A.Application.Options;
using E3A.Domain.Reports;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace E3A.Application.Reports.SubmitReport;

public sealed class SubmitReportValidator : AbstractValidator<SubmitReportCommand>
{
    public SubmitReportValidator(IOptions<ReportsOptions> reportsOptions)
    {
        var options = reportsOptions.Value;

        RuleFor(x => x.ItemId).ValidateRequired(ErrorCodes.ReportItemIdRequired);

        RuleFor(x => x.ItemType).IsInEnum().WithErrorCode(ErrorCodes.ReportItemTypeInvalid);

        RuleFor(x => x.Reason).IsInEnum().WithErrorCode(ErrorCodes.ReportReasonInvalid);

        RuleFor(x => x.Details).ValidateMaxLength(options.DetailsMaxLength, ErrorCodes.ReportDetailsTooLong);

        RuleFor(x => x.Details)
            .ValidateRequired(ErrorCodes.ReportDetailsRequired)
            .When(x => x.Reason == ReportReason.Other);
    }
}
