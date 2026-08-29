namespace E3A.Application.Authentication.Shared;

public sealed record GitHubLoginUrlResult(string RedirectUrl, string StateNonce);
