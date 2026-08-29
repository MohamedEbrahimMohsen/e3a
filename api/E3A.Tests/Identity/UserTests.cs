using E3A.Tests.Identity.Shared;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Identity;

public sealed class UserTests
{
    [Fact]
    public void CreateFromGitHub_ShouldSetGitHubIdentity_WhenCalled()
    {
        var user = UserFactory.GitHub();

        user.GitHubId.Should().Be(UserFactory.DefaultGitHubId);
        user.GitHubLogin.Should().Be(UserFactory.DefaultLogin);
        user.DisplayName.Should().Be(UserFactory.DefaultDisplayName);
        user.AvatarUrl.Should().Be(UserFactory.DefaultAvatarUrl);
        user.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void CreateFromGitHub_ShouldSetUserNameAndNormalizedUserNameFromTheResolvedName_WhenItDiffersFromTheLogin()
    {
        var user = UserFactory.GitHub(login: "OctoCat", userName: "OctoCat-ab12");

        user.GitHubLogin.Should().Be("OctoCat");
        user.UserName.Should().Be("OctoCat-ab12");
        user.NormalizedUserName.Should().Be("OCTOCAT-AB12");
    }

    [Fact]
    public void CreateFromGitHub_ShouldSetSecurityStamp_WhenCalled()
    {
        var user = UserFactory.GitHub();

        user.SecurityStamp.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void CreateFromGitHub_ShouldStampCreationAndUpdationDates_WhenCalled()
    {
        var before = DateTimeOffset.UtcNow;

        var user = UserFactory.GitHub();

        user.CreationDate.Should().BeOnOrAfter(before);
        user.UpdationDate.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void CreateFromGitHub_ShouldLeaveEmailUnset_WhenCalled()
    {
        var user = UserFactory.GitHub();

        user.Email.Should().BeNull();
        user.NormalizedEmail.Should().BeNull();
    }

    [Fact]
    public void UpdateGitHubProfile_ShouldReplaceDisplayNameAndAvatar_WhenCalled()
    {
        var user = UserFactory.GitHub();

        user.UpdateGitHubProfile("Renamed Octocat", "https://avatars.githubusercontent.com/u/9999");
        user.DisplayName.Should().Be("Renamed Octocat");
        user.AvatarUrl.Should().Be("https://avatars.githubusercontent.com/u/9999");

        user.UpdateGitHubProfile(null, null);
        user.DisplayName.Should().BeNull();
        user.AvatarUrl.Should().BeNull();
    }

    [Fact]
    public void UpdateGitHubProfile_ShouldAdvanceUpdationDate_WhenCalled()
    {
        var user = UserFactory.GitHub();
        var before = DateTimeOffset.UtcNow;

        user.UpdateGitHubProfile("Renamed Octocat", null);

        user.UpdationDate.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void UpdateGitHubProfile_ShouldNotChangeGitHubIdentity_WhenCalled()
    {
        var user = UserFactory.GitHub();

        user.UpdateGitHubProfile("Renamed Octocat", null);

        user.GitHubId.Should().Be(UserFactory.DefaultGitHubId);
        user.GitHubLogin.Should().Be(UserFactory.DefaultLogin);
        user.UserName.Should().Be(UserFactory.DefaultLogin);
        user.NormalizedUserName.Should().Be(UserFactory.DefaultLogin.ToUpperInvariant());
    }
}
