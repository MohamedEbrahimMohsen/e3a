using E3A.Application.Authentication.Shared;
using E3A.Application.Options;
using MediatR;
using Microsoft.Extensions.Options;

namespace E3A.Application.Authentication.GetGitHubLoginUrl;

public sealed class GetGitHubLoginUrlQueryHandler(IOAuthStateProtector oAuthStateProtector, IOptions<GitHubAuthenticationOptions> gitHubAuthenticationOptions) : IRequestHandler<GetGitHubLoginUrlQuery, GitHubLoginUrlResult>
{
    public Task<GitHubLoginUrlResult> Handle(GetGitHubLoginUrlQuery request, CancellationToken cancellationToken)
    {
        var state = oAuthStateProtector.Create();
        var authorizationUrl = GitHubAuthorizationUrlGenerator.Generate(gitHubAuthenticationOptions.Value, state.Value);

        return Task.FromResult(new GitHubLoginUrlResult(authorizationUrl, state.Nonce));
    }
}
