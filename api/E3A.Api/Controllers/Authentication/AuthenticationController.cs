using E3A.Application.Authentication.CompleteGitHubLogin;
using E3A.Application.Authentication.GetCurrentUser;
using E3A.Application.Authentication.GetGitHubLoginUrl;
using E3A.Application.Options;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace E3A.Api.Controllers.Authentication;

[ApiController]
[Route("api/auth")]
[Authorize]
public class AuthenticationController(IMediator mediator, IOptions<GitHubAuthenticationOptions> gitHubAuthenticationOptions) : ControllerBase
{
    [HttpGet("github/login")]
    [AllowAnonymous]
    public async Task<ActionResult> GetGitHubLoginUrl(CancellationToken cancellationToken)
    {
        var options = gitHubAuthenticationOptions.Value;
        var result = await mediator.Send(new GetGitHubLoginUrlQuery(), cancellationToken);

        Response.Cookies.Append(options.StateCookieName, result.StateNonce, OAuthStateCookieOptionsGenerator.Generate(TimeSpan.FromMinutes(options.StateExpirationMinutes)));

        return Redirect(result.RedirectUrl);
    }

    [HttpGet("github/callback")]
    [AllowAnonymous]
    public async Task<ActionResult> CompleteGitHubLogin([FromQuery] string? code, [FromQuery] string? state, CancellationToken cancellationToken)
    {
        var options = gitHubAuthenticationOptions.Value;
        var nonce = Request.Cookies[options.StateCookieName];

        Response.Cookies.Delete(options.StateCookieName, OAuthStateCookieOptionsGenerator.Generate());

        var result = await mediator.Send(new CompleteGitHubLoginCommand(code, state, nonce), cancellationToken);

        return Redirect(result.RedirectUrl);
    }

    [HttpGet("me")]
    public async Task<ActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetCurrentUserQuery(), cancellationToken);
        return Ok(result);
    }
}
