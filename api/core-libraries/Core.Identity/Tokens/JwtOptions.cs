namespace Core.Identity.Tokens;

public sealed class JwtOptions
{
    public const string SectionName = "CoreJwt";

    // Access Token
    public string Issuer { get; set; } = default!;
    public string Audience { get; set; } = default!;
    public string Key { get; set; } = default!;
    public int ExpirationHours { get; set; } = 72;

    // Refresh Token
    public int RefreshTokenExpirationDays { get; set; } = 7;
    public int RefreshTokenLength { get; set; } = 64; // bytes before Base64
}
