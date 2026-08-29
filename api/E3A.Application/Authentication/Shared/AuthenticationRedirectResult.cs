namespace E3A.Application.Authentication.Shared;

public sealed record AuthenticationRedirectResult(string RedirectUrl, bool StateNonceConsumed);
