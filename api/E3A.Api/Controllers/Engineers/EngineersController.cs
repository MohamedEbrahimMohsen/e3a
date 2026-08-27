using E3A.Application.Engineers.CreateEngineer;
using E3A.Application.Engineers.DeleteEngineer;
using E3A.Application.Engineers.GetEngineer;
using E3A.Application.Engineers.ListEngineers;
using E3A.Application.Engineers.ListMyEngineers;
using E3A.Application.Engineers.UpdateEngineer;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace E3A.Api.Controllers.Engineers;

[ApiController]
[Route("api/engineers")]
[Authorize]
public class EngineersController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult> ListEngineers(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ListEngineersQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("mine")]
    public async Task<ActionResult> ListMyEngineers(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ListMyEngineersQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{engineerId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult> GetEngineer([FromRoute] Guid engineerId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetEngineerQuery(engineerId), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult> CreateEngineer([FromBody] CreateEngineerRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new CreateEngineerCommand(request.DisplayName, request.Description, request.Tags ?? []), cancellationToken);
        return CreatedAtAction(nameof(GetEngineer), new { engineerId = result.Id }, result);
    }

    [HttpPut("{engineerId:guid}")]
    public async Task<ActionResult> UpdateEngineer([FromRoute] Guid engineerId, [FromBody] UpdateEngineerRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new UpdateEngineerCommand(engineerId, request.DisplayName, request.Description, request.Tags ?? []), cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{engineerId:guid}")]
    public async Task<ActionResult> DeleteEngineer([FromRoute] Guid engineerId, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteEngineerCommand(engineerId), cancellationToken);
        return NoContent();
    }
}
