using System.Security.Claims;

namespace Core.Identity.Tokens.AccessToken;

public interface ITokenService
{
    string GenerateTokenAsync(List<Claim> claims);
    Task<string> GenerateTokenAsync(string refreshToken, List<Claim> claims);
}
