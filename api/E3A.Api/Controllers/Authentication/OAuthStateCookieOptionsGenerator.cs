namespace E3A.Api.Controllers.Authentication;

public static class OAuthStateCookieOptionsGenerator
{
    // Must stay equal to the controller route so the browser returns the cookie on the callback and drops it on deletion.
    private const string AuthenticationPath = "/api/auth";

    public static CookieOptions Generate(TimeSpan? maxAge = null)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Path = AuthenticationPath,
            MaxAge = maxAge,
        };
    }
}
