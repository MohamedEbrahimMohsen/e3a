using E3A.Application.Options;
using Microsoft.AspNetCore.WebUtilities;

namespace E3A.Application.Authentication.Shared;

public static class GitHubAuthorizationUrlGenerator
{
    public static string Generate(GitHubAuthenticationOptions options, string state)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["client_id"] = options.ClientId,
            ["redirect_uri"] = options.CallbackUrl,
            ["scope"] = options.Scope,
            ["state"] = state,
        };

        return QueryHelpers.AddQueryString(options.AuthorizationUrl, parameters);
    }
}
