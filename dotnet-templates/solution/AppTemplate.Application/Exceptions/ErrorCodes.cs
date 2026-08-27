namespace AppTemplate.Application.Exceptions;

/// <summary>
/// Flat error-code registry, grouped by area. Adding a code here is only the
/// first of the places the constitution requires — complete every one.
/// </summary>
public static class ErrorCodes
{
    // Identity
    public const string UserNotAuthenticated = "USER_NOT_AUTHENTICATED";
    public const string UserNotFound = "USER_NOT_FOUND";
}
