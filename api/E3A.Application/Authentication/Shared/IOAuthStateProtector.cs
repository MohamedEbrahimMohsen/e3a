namespace E3A.Application.Authentication.Shared;

public interface IOAuthStateProtector
{
    string Create();
    OAuthStateStatus Validate(string? state);
}
