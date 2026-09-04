namespace E3A.Application.Authentication.Shared;

public interface IGitHubOAuthClient
{
    Task<string?> ExchangeCodeForAccessTokenAsync(string code, CancellationToken cancellationToken);
    Task<GitHubProfile?> GetProfileAsync(string accessToken, CancellationToken cancellationToken);
}
