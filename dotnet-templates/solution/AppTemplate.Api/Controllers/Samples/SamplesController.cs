using AppTemplate.Application.Samples.AddSample;
using AppTemplate.Application.Samples.ListSamples;
using AppTemplate.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppTemplate.Api.Controllers.Samples;

[ApiController]
[Route("[controller]")]
[Authorize(Roles = RoleNames.Admin)]
public class SamplesController(IMediator mediator) : ControllerBase
{
    // The collection action MUST be declared before the single-record action,
    // or the route parameter captures the literal segment.
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult> ListSamples([FromQuery] bool activeOnly, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ListSamplesQuery(activeOnly), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult> AddSample([FromBody] AddSampleCommand request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(request, cancellationToken);
        return Ok(result);
    }
}
