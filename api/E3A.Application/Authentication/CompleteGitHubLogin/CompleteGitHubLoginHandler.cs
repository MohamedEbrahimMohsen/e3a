using Core.Identity.Tokens.AccessToken;
using Core.Utilities.Generator;
using E3A.Application.Authentication.Shared;
using E3A.Application.Exceptions;
using E3A.Application.Options;
using E3A.Domain.Identity;
using MediatR;
using Microsoft.Extensions.Options;

namespace E3A.Application.Authentication.CompleteGitHubLogin;

public sealed class CompleteGitHubLoginHandler(IGitHubOAuthClient gitHubOAuthClient, IOAuthStateProtector oAuthStateProtector, IUserRepository userRepository, ITokenService tokenService, IGenerator generator, IOptions<GitHubAuthenticationOptions> gitHubAuthenticationOptions) : IRequestHandler<CompleteGitHubLoginCommand, AuthenticationRedirectResult>
{
    public async Task<AuthenticationRedirectResult> Handle(CompleteGitHubLoginCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return Failure(ErrorCodes.AuthenticationCodeMissing, stateNonceConsumed: false);
        }

        var stateStatus = oAuthStateProtector.Validate(request.State, request.Nonce);

        if (stateStatus == OAuthStateStatus.Invalid)
        {
            return Failure(ErrorCodes.AuthenticationStateInvalid, stateNonceConsumed: false);
        }

        if (stateStatus == OAuthStateStatus.Expired)
        {
            return Failure(ErrorCodes.AuthenticationStateExpired, stateNonceConsumed: true);
        }

        var accessToken = await gitHubOAuthClient.ExchangeCodeForAccessTokenAsync(request.Code, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Failure(ErrorCodes.GitHubTokenExchangeFailed, stateNonceConsumed: true);
        }

        var profile = await gitHubOAuthClient.GetProfileAsync(accessToken, cancellationToken).ConfigureAwait(false);

        if (profile == null)
        {
            return Failure(ErrorCodes.GitHubProfileFetchFailed, stateNonceConsumed: true);
        }

        if (profile.Id <= 0 || string.IsNullOrWhiteSpace(profile.Login))
        {
            return Failure(ErrorCodes.GitHubProfileInvalid, stateNonceConsumed: true);
        }

        var user = await userRepository.FirstOrDefaultAsync(x => x.GitHubId == profile.Id, cancellationToken).ConfigureAwait(false);

        if (user == null)
        {
            var userName = await UserNameResolver.ResolveUniqueAsync(profile.Login, userRepository, generator, gitHubAuthenticationOptions.Value, cancellationToken).ConfigureAwait(false);
            user = User.CreateFromGitHub(profile.Id, profile.Login, userName, profile.Name, profile.AvatarUrl);
            await userRepository.AddAsync(user, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            user.UpdateGitHubProfile(profile.Name, profile.AvatarUrl);
            userRepository.Update(user);
        }

        await userRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var token = tokenService.GenerateTokenAsync(UserClaimsGenerator.Generate(user));

        return new AuthenticationRedirectResult(AuthenticationRedirectUrlGenerator.Success(gitHubAuthenticationOptions.Value.WebRedirectUrl, token), StateNonceConsumed: true);
    }

    private AuthenticationRedirectResult Failure(string errorCode, bool stateNonceConsumed)
    {
        return new AuthenticationRedirectResult(AuthenticationRedirectUrlGenerator.Failure(gitHubAuthenticationOptions.Value.WebRedirectUrl, errorCode), stateNonceConsumed);
    }
}
