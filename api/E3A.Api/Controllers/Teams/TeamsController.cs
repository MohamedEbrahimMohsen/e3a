using E3A.Application.Teams.CheckTeamSlugAvailability;
using E3A.Application.Teams.CreateTeam;
using E3A.Application.Teams.DeleteTeam;
using E3A.Application.Teams.GetTeam;
using E3A.Application.Teams.ListMyTeams;
using E3A.Application.Teams.PublishTeam;
using E3A.Application.Teams.SetTeamMembers;
using E3A.Application.Teams.UpdateTeam;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace E3A.Api.Controllers.Teams;

[ApiController]
[Route("api/teams")]
[Authorize]
public class TeamsController(IMediator mediator) : ControllerBase
{
    [HttpGet("mine")]
    public async Task<ActionResult> ListMyTeams(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ListMyTeamsQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("slug-availability")]
    public async Task<ActionResult> CheckTeamSlugAvailability([FromQuery] string slug, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new CheckTeamSlugAvailabilityQuery(slug), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{teamId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult> GetTeam([FromRoute] Guid teamId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetTeamQuery(teamId), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult> CreateTeam([FromBody] CreateTeamRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new CreateTeamCommand(request.Slug, request.DisplayName, request.Description, request.Tags ?? []), cancellationToken);
        return CreatedAtAction(nameof(GetTeam), new { teamId = result.Id }, result);
    }

    [HttpPut("{teamId:guid}")]
    public async Task<ActionResult> UpdateTeam([FromRoute] Guid teamId, [FromBody] UpdateTeamRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new UpdateTeamCommand(teamId, request.Slug, request.DisplayName, request.Description, request.Tags ?? []), cancellationToken);
        return Ok(result);
    }

    [HttpPut("{teamId:guid}/members")]
    public async Task<ActionResult> SetTeamMembers([FromRoute] Guid teamId, [FromBody] SetTeamMembersRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new SetTeamMembersCommand(teamId, [.. (request.Members ?? []).Select(x => new TeamMemberSelection(x.EngineerId, x.PinnedVersionId))]), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{teamId:guid}/publish")]
    public async Task<ActionResult> PublishTeam([FromRoute] Guid teamId, [FromBody] PublishTeamRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new PublishTeamCommand(teamId, request.Increment), cancellationToken);
        return Accepted(result);
    }

    [HttpDelete("{teamId:guid}")]
    public async Task<ActionResult> DeleteTeam([FromRoute] Guid teamId, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteTeamCommand(teamId), cancellationToken);
        return NoContent();
    }
}
