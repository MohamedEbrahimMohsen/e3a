namespace E3A.Application.Authentication.Shared;

public static class AuthenticationRedirectUrlGenerator
{
    private const string TokenFragmentKey = "token";
    private const string ErrorFragmentKey = "error";

    public static string Success(string webRedirectUrl, string token)
    {
        return $"{webRedirectUrl}#{TokenFragmentKey}={Uri.EscapeDataString(token)}";
    }

    public static string Failure(string webRedirectUrl, string errorCode)
    {
        return $"{webRedirectUrl}#{ErrorFragmentKey}={Uri.EscapeDataString(errorCode)}";
    }
}
