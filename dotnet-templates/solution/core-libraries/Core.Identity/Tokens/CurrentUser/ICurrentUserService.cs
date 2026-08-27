namespace Core.Identity.Tokens.CurrentUser;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? NationalId { get; }
    string? UserName { get; }
    string? PhoneNumber { get; }
    string? LoginType { get; }
    long? CreatedAtUnixTimeSeconds { get; }
    string? GetClaim(string claimName);
}
