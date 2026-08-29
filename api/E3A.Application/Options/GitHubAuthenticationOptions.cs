namespace E3A.Application.Options;

public sealed class GitHubAuthenticationOptions
{
    public const string SectionName = "GitHubAuthentication";

    public string AppId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string AuthorizationUrl { get; set; } = string.Empty;
    public string AccessTokenUrl { get; set; } = string.Empty;
    public string UserProfileUrl { get; set; } = string.Empty;
    public string CallbackUrl { get; set; } = string.Empty;
    public string WebRedirectUrl { get; set; } = string.Empty;
    public int StateExpirationMinutes { get; set; } = 10;
    public string Scope { get; set; } = "read:user";
    public string UserAgent { get; set; } = "e3a";
    public int HttpTimeoutSeconds { get; set; } = 10;
    public int StateNonceSize { get; set; } = 16;
    public int GitHubLoginMaxLength { get; set; } = 100;
    public int DisplayNameMaxLength { get; set; } = 200;
    public int AvatarUrlMaxLength { get; set; } = 500;
}
