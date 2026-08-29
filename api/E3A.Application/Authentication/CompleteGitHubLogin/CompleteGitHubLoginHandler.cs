using Core.Identity.Tokens.AccessToken;
using E3A.Application.Authentication.Shared;
using E3A.Application.Exceptions;
using E3A.Application.Options;
using E3A.Domain.Identity;
using MediatR;
using Microsoft.Extensions.Options;

namespace E3A.Application.Authentication.CompleteGitHubLogin;

public sealed class CompleteGitHubLoginHandler(IGitHubOAuthClient gitHubOAuthClient, IOAuthStateProtector oAuthStateProtector, IUserRepository userRepository, ITokenService tokenService, IOptions<GitHubAuthenticationOptions> gitHubAuthenticationOptions) : IRequestHandler<CompleteGitHubLoginCommand, AuthenticationRedirectResult>
{
    public async Task<AuthenticationRedirectResult> Handle(CompleteGitHubLoginCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return Failure(ErrorCodes.AuthenticationCodeMissing);
        }

        var stateStatus = oAuthStateProtector.Validate(request.State);

        if (stateStatus == OAuthStateStatus.Invalid)
        {
            return Failure(ErrorCodes.AuthenticationStateInvalid);
        }

        if (stateStatus == OAuthStateStatus.Expired)
        {
            return Failure(ErrorCodes.AuthenticationStateExpired);
        }

        var accessToken = await gitHubOAuthClient.ExchangeCodeForAccessTokenAsync(request.Code, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Failure(ErrorCodes.GitHubTokenExchangeFailed);
        }

        var profile = await gitHubOAuthClient.GetProfileAsync(accessToken, cancellationToken).ConfigureAwait(false);

        if (profile == null)
        {
            return Failure(ErrorCodes.GitHubProfileFetchFailed);
        }

        if (profile.Id <= 0 || string.IsNullOrWhiteSpace(profile.Login))
        {
            return Failure(ErrorCodes.GitHubProfileInvalid);
        }

        var user = await userRepository.FirstOrDefaultAsync(x => x.GitHubId == profile.Id, cancellationToken).ConfigureAwait(false);

        if (user == null)
        {
            user = User.CreateFromGitHub(profile.Id, profile.Login, profile.Name, profile.AvatarUrl);
            await userRepository.AddAsync(user, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            user.UpdateGitHubProfile(profile.Name, profile.AvatarUrl);
            userRepository.Update(user);
        }

        await userRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var token = tokenService.GenerateTokenAsync(UserClaimsGenerator.Generate(user));

        return new AuthenticationRedirectResult(AuthenticationRedirectUrlGenerator.Success(gitHubAuthenticationOptions.Value.WebRedirectUrl, token));
    }

    private AuthenticationRedirectResult Failure(string errorCode)
    {
        return new AuthenticationRedirectResult(AuthenticationRedirectUrlGenerator.Failure(gitHubAuthenticationOptions.Value.WebRedirectUrl, errorCode));
    }
}
