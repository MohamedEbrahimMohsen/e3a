using Core.Identity.Tokens.CurrentUser;
using E3A.Domain.Identity;
using System.Globalization;
using System.Security.Claims;

namespace E3A.Application.Authentication.Shared;

public static class UserClaimsGenerator
{
    // The only login path e3a has; surfaced downstream as ICurrentUserService.LoginType.
    public const string GitHubLoginType = "GitHub";

    public static List<Claim> Generate(User user)
    {
        return
        [
            new Claim(CurrentUserService.Constants.UserIdClaimType, user.Id.ToString()),
            new Claim(CurrentUserService.Constants.UserNameClaimType, user.UserName ?? string.Empty),
            new Claim(CurrentUserService.Constants.LoginTypeClaimType, GitHubLoginType),
            new Claim(CurrentUserService.Constants.CreatedAtUnixTimeSecondsClaimType, user.CreationDate.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
        ];
    }
}
