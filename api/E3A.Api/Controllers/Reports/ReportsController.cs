using E3A.Application.Reports.SubmitReport;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace E3A.Api.Controllers.Reports;

[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportsController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult> SubmitReport([FromBody] SubmitReportRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new SubmitReportCommand(request.ItemType, request.ItemId, request.Reason, request.Details), cancellationToken);
        return Ok(result);
    }
}
