using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using E3A.Application.Exceptions;
using E3A.Application.Options;
using E3A.Application.Reports.Shared;
using E3A.Domain.Engineers;
using E3A.Domain.Publishing;
using E3A.Domain.Reports;
using E3A.Domain.Teams;
using MediatR;
using Microsoft.Extensions.Options;

namespace E3A.Application.Reports.SubmitReport;

public sealed class SubmitReportHandler(IReportRepository reportRepository, IEngineerRepository engineerRepository, ITeamRepository teamRepository, ICurrentUserService currentUserService, IOptions<ReportsOptions> reportsOptions) : IRequestHandler<SubmitReportCommand, ReportResult>
{
    public async Task<ReportResult> Handle(SubmitReportCommand request, CancellationToken cancellationToken)
    {
        var options = reportsOptions.Value;

        var itemExists = request.ItemType switch
        {
            ItemType.Engineer => await engineerRepository.GetByIdAsync(request.ItemId, cancellationToken, asNoTracking: true).ConfigureAwait(false) is not null,
            _ => await teamRepository.GetByIdAsync(request.ItemId, cancellationToken, asNoTracking: true).ConfigureAwait(false) is not null,
        };

        if (!itemExists)
        {
            throw new BadRequestCoreException(ErrorCodes.ReportItemNotFound);
        }

        var existingReportCount = await reportRepository.CountAsync(cancellationToken, x => x.ItemType == request.ItemType && x.ItemId == request.ItemId).ConfigureAwait(false);

        if (existingReportCount >= options.MaxReportsPerItem)
        {
            throw new RateLimitExceededCoreException(ErrorCodes.ReportLimitReached, context: new Dictionary<string, object> { ["limit"] = options.MaxReportsPerItem });
        }

        var reporterUserId = currentUserService.UserId == Guid.Empty ? null : currentUserService.UserId;
        var report = Report.Create(request.ItemType, request.ItemId, reporterUserId, request.Reason, request.Details);

        await reportRepository.AddAsync(report, cancellationToken).ConfigureAwait(false);
        await reportRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ReportResultGenerator.Generate(report);
    }
}
