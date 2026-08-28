using E3A.Application.Publishing.GetPublishStatus;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace E3A.Api.Controllers.Publishing;

[ApiController]
[Route("api/publish")]
[Authorize]
public class PublishController(IMediator mediator) : ControllerBase
{
    [HttpGet("{versionId:guid}/status")]
    public async Task<ActionResult> GetPublishStatus([FromRoute] Guid versionId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetPublishStatusQuery(versionId), cancellationToken);
        return Ok(result);
    }
}
