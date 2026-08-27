using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Core.Identity.Tokens.CurrentUser;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public static class Constants
    {
        public const string UserIdClaimType = ClaimTypes.NameIdentifier;
        public const string UserNameClaimType = ClaimTypes.Name;
        public const string PhoneNumberClaimType = ClaimTypes.MobilePhone;
        public const string NationalIdClaimType = "nid";
        public const string LoginTypeClaimType = "login_type";
        public const string CreatedAtUnixTimeSecondsClaimType = "created_at_unix_seconds";
    }

    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;
    public Guid? UserId => Guid.TryParse(User?.FindFirst(Constants.UserIdClaimType)?.Value, out var id) ? id : null;
    public string? UserName => User?.FindFirst(Constants.UserNameClaimType)?.Value;
    public string? PhoneNumber => User?.FindFirst(Constants.PhoneNumberClaimType)?.Value;
    public string? NationalId => User?.FindFirst(Constants.NationalIdClaimType)?.Value;
    public string? LoginType => User?.FindFirst(Constants.LoginTypeClaimType)?.Value;
    public long? CreatedAtUnixTimeSeconds => long.TryParse(User?.FindFirst(Constants.CreatedAtUnixTimeSecondsClaimType)?.Value, out var unixCreatedAt) ? unixCreatedAt : null;
    public string? GetClaim(string claimName) => User?.FindFirst(claimName)?.Value;
}