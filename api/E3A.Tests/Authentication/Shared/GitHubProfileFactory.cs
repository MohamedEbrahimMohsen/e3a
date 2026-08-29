using E3A.Application.Authentication.Shared;

namespace E3A.Tests.Authentication.Shared;

public static class GitHubProfileFactory
{
    public static GitHubProfile Default(long id = 4242, string login = "octocat", string? name = "The Octocat", string? avatarUrl = "https://avatars.githubusercontent.com/u/4242")
    {
        return new GitHubProfile(id, login, name, avatarUrl);
    }
}
