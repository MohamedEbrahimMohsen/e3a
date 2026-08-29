using E3A.Application.Authentication.CompleteGitHubLogin;
using E3A.Application.Authentication.GetCurrentUser;
using E3A.Application.Authentication.GetGitHubLoginUrl;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace E3A.Api.Controllers.Authentication;

[ApiController]
[Route("api/auth")]
[Authorize]
public class AuthenticationController(IMediator mediator) : ControllerBase
{
    [HttpGet("github/login")]
    [AllowAnonymous]
    public async Task<ActionResult> GetGitHubLoginUrl(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetGitHubLoginUrlQuery(), cancellationToken);
        return Redirect(result.RedirectUrl);
    }

    [HttpGet("github/callback")]
    [AllowAnonymous]
    public async Task<ActionResult> CompleteGitHubLogin([FromQuery] string? code, [FromQuery] string? state, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new CompleteGitHubLoginCommand(code, state), cancellationToken);
        return Redirect(result.RedirectUrl);
    }

    [HttpGet("me")]
    public async Task<ActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetCurrentUserQuery(), cancellationToken);
        return Ok(result);
    }
}
