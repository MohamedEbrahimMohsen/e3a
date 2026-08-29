using E3A.Application.Options;

namespace E3A.Tests.Authentication.Shared;

public static class GitHubAuthenticationOptionsFactory
{
    public const string AuthorizationUrl = "https://github.com/login/oauth/authorize";
    public const string CallbackUrl = "https://localhost:62935/api/auth/github/callback";
    public const string WebRedirectUrl = "http://localhost:5174/auth/callback";
    public const string ClientId = "Iv1TestClientId";
    public const string Scope = "read:user";

    public static GitHubAuthenticationOptions Default(int stateExpirationMinutes = 10)
    {
        return new GitHubAuthenticationOptions
        {
            AppId = "1234567",
            ClientId = ClientId,
            ClientSecret = "dummy-client-secret",
            AuthorizationUrl = AuthorizationUrl,
            AccessTokenUrl = "https://github.com/login/oauth/access_token",
            UserProfileUrl = "https://api.github.com/user",
            CallbackUrl = CallbackUrl,
            WebRedirectUrl = WebRedirectUrl,
            StateExpirationMinutes = stateExpirationMinutes,
            Scope = Scope,
            UserAgent = "e3a",
            HttpTimeoutSeconds = 10,
            StateNonceSize = 16,
            GitHubLoginMaxLength = 100,
            DisplayNameMaxLength = 200,
            AvatarUrlMaxLength = 500,
        };
    }
}
