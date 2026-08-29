using E3A.Domain.Identity;

namespace E3A.Tests.Identity.Shared;

public static class UserFactory
{
    public const long DefaultGitHubId = 4242;
    public const string DefaultLogin = "octocat";
    public const string DefaultDisplayName = "The Octocat";
    public const string DefaultAvatarUrl = "https://avatars.githubusercontent.com/u/4242";

    public static User GitHub(long gitHubId = DefaultGitHubId, string login = DefaultLogin, string? displayName = DefaultDisplayName, string? avatarUrl = DefaultAvatarUrl)
    {
        return User.CreateFromGitHub(gitHubId, login, displayName, avatarUrl);
    }
}
