using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Notifications.Exceptions;
using Microsoft.Extensions.Configuration;

namespace Core.Notifications.Templates.Shared;

public static class NotificationTemplateClaimsChecker
{
    public static void ValidateRequiredClaims(string requiredClaimConfigurationKey, IConfiguration configuration, ICurrentUserService currentUserService)
    {
        var isRequiredClaims = configuration.GetValue<bool>(requiredClaimConfigurationKey);

        if (isRequiredClaims)
        {
            var claims = configuration.GetValue<Dictionary<string, string>>("CoreNotifications:NotificationTemplateRequiredClaims") ?? [];
            var isAuthorized = false;

            foreach (var claim in claims)
            {
                var claimValue = currentUserService.GetClaim(claim.Key);
                if (!string.IsNullOrEmpty(claimValue) && claimValue.ToUpper() == claim.Value.ToUpper())
                {
                    isAuthorized = true;
                    break;
                }
            }

            if (!isAuthorized)
            {
                throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
            }
        }
    }
}
