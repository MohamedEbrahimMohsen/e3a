using E3A.Application.Engineers.CheckSlugAvailability;
using E3A.Application.Engineers.CreateEngineer;
using E3A.Application.Engineers.DeleteEngineer;
using E3A.Application.Engineers.GetEngineer;
using E3A.Application.Engineers.GetImportManifest;
using E3A.Application.Engineers.ListMyEngineers;
using E3A.Application.Engineers.PublishEngineer;
using E3A.Application.Engineers.RelistEngineer;
using E3A.Application.Engineers.UnlistEngineer;
using E3A.Application.Engineers.UpdateEngineer;
using E3A.Application.Engineers.UploadEngineerDraft;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace E3A.Api.Controllers.Engineers;

[ApiController]
[Route("api/engineers")]
[Authorize]
public class EngineersController(IMediator mediator) : ControllerBase
{
    [HttpGet("mine")]
    public async Task<ActionResult> ListMyEngineers(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ListMyEngineersQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("slug-availability")]
    public async Task<ActionResult> CheckSlugAvailability([FromQuery] string slug, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new CheckSlugAvailabilityQuery(slug), cancellationToken);
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
        var result = await mediator.Send(new CreateEngineerCommand(request.Slug, request.DisplayName, request.Description, request.Tags ?? []), cancellationToken);
        return CreatedAtAction(nameof(GetEngineer), new { engineerId = result.Id }, result);
    }

    [HttpPut("{engineerId:guid}")]
    public async Task<ActionResult> UpdateEngineer([FromRoute] Guid engineerId, [FromBody] UpdateEngineerRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new UpdateEngineerCommand(engineerId, request.Slug, request.DisplayName, request.Description, request.Tags ?? []), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{engineerId:guid}/upload")]
    public async Task<ActionResult> UploadEngineerDraft([FromRoute] Guid engineerId, [FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new UploadEngineerDraftCommand(engineerId, file), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{engineerId:guid}/import-manifest")]
    public async Task<ActionResult> GetImportManifest([FromRoute] Guid engineerId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetImportManifestQuery(engineerId), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{engineerId:guid}/publish")]
    public async Task<ActionResult> PublishEngineer([FromRoute] Guid engineerId, [FromBody] PublishEngineerRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new PublishEngineerCommand(engineerId, request.Increment), cancellationToken);
        return Accepted(result);
    }

    [HttpPost("{engineerId:guid}/unlist")]
    public async Task<ActionResult> UnlistEngineer([FromRoute] Guid engineerId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new UnlistEngineerCommand(engineerId), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{engineerId:guid}/relist")]
    public async Task<ActionResult> RelistEngineer([FromRoute] Guid engineerId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new RelistEngineerCommand(engineerId), cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{engineerId:guid}")]
    public async Task<ActionResult> DeleteEngineer([FromRoute] Guid engineerId, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteEngineerCommand(engineerId), cancellationToken);
        return NoContent();
    }
}
