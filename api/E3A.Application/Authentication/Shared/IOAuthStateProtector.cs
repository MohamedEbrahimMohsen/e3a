namespace E3A.Application.Authentication.Shared;

public interface IOAuthStateProtector
{
    OAuthState Create();
    OAuthStateStatus Validate(string? state, string? nonce);
}
