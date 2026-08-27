using Microsoft.AspNetCore.Identity;

namespace Core.Identity.Tokens.RefreshToken;

public interface IRefreshTokenService<TUser, TKey> where TUser : IdentityUser<TKey>, new() where TKey : IEquatable<TKey>
{
    Task<string> GenerateTokenAsync(TUser user, CancellationToken cancellationToken);
}
