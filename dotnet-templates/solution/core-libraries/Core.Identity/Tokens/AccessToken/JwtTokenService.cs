using Core.DDD.Entities;
using Core.Errors;
using Core.Identity.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Timers;
using System.Xml.Linq;

namespace Core.Identity.Tokens.AccessToken;

public class JwtTokenService<TUser, TKey>(SignInManager<TUser> signInManager, IOptions<JwtOptions> jwtOptions, IOptionsMonitor<BearerTokenOptions> bearerOptions) : ITokenService where TUser : IdentityUser<TKey>, IEntity, new() where TKey : IEquatable<TKey>
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public string GenerateTokenAsync(List<Claim> claims)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));
        var signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: DateTimeOffset.UtcNow.AddHours(_jwt.ExpirationHours).UtcDateTime,
            signingCredentials: signingCredentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<string> GenerateTokenAsync(string refreshToken, List<Claim> claims)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserCreationFailed);
        }

        var ticket = bearerOptions.Get(IdentityConstants.BearerScheme).RefreshTokenProtector
                                  .Unprotect(refreshToken);

        if (ticket?.Properties?.ExpiresUtc is null || ticket.Properties.ExpiresUtc < DateTimeOffset.UtcNow)
        {
            throw new UnauthorizedCoreException(ErrorCodes.RefreshTokenIsExpired);
        }

        var user = await signInManager.ValidateSecurityStampAsync(ticket.Principal);
        if (user == null || user.IsDeleted)
        {
            throw new UnauthorizedCoreException(ErrorCodes.RefreshTokenUserNotFound);
        }

        return GenerateTokenAsync(claims);
    }

}
