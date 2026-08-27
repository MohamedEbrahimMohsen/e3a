using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Core.Identity.Tokens.RefreshToken;

public class RefreshTokenService<TUser, TKey>(SignInManager<TUser> signInManager, IOptionsMonitor<BearerTokenOptions> bearerOptions, IOptions<JwtOptions> jwtOptions) : IRefreshTokenService<TUser, TKey> where TUser : IdentityUser<TKey>, new() where TKey : IEquatable<TKey>
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public async Task<string> GenerateTokenAsync(TUser user, CancellationToken cancellationToken)
    {
        var principal = await signInManager.CreateUserPrincipalAsync(user).ConfigureAwait(false);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(_jwt.RefreshTokenExpirationDays)
        };

        var refreshToken = bearerOptions.Get(IdentityConstants.BearerScheme).RefreshTokenProtector
                                        .Protect(new AuthenticationTicket(principal, authProperties, IdentityConstants.BearerScheme));

        return refreshToken;
    }
}
