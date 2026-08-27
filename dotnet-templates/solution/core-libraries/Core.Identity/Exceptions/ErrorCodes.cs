namespace Core.Identity.Exceptions;

public static class ErrorCodes
{
    public const string UserCreationFailed = "USER_CREATION_FAILED";
    public const string UserIsRequired = "USER_IS_REQUIRED";
    public const string PasswordIsRequired = "PASSWORD_IS_REQUIRED";
    public const string RefreshTokenIsRequired = "REFRESH_TOKEN_IS_REQUIRED";
    public const string RefreshTokenIsExpired = "REFRESH_TOKEN_IS_EXPIRED";
    public const string RefreshTokenUserNotFound = "REFRESH_TOKEN_USER_NOT_FOUND";
}
