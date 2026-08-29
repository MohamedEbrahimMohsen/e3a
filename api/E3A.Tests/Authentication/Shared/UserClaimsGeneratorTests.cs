using Core.Identity.Tokens.CurrentUser;
using E3A.Application.Authentication.Shared;
using E3A.Tests.Identity.Shared;
using FluentAssertions;
using System.Globalization;
using System.Security.Claims;
using Xunit;

namespace E3A.Tests.Authentication.Shared;

public sealed class UserClaimsGeneratorTests
{
    [Fact]
    public void Generate_ShouldEmitAUserIdClaimCurrentUserServiceCanParse_WhenCalled()
    {
        var user = UserFactory.GitHub();

        var claims = UserClaimsGenerator.Generate(user);

        var value = claims.Single(x => x.Type == CurrentUserService.Constants.UserIdClaimType).Value;
        Guid.Parse(value).Should().Be(user.Id);
    }

    [Fact]
    public void Generate_ShouldEmitTheUserNameClaim_WhenCalled()
    {
        var user = UserFactory.GitHub();

        var claims = UserClaimsGenerator.Generate(user);

        claims.Should().Contain(x => x.Type == CurrentUserService.Constants.UserNameClaimType && x.Value == user.UserName);
    }

    [Fact]
    public void Generate_ShouldEmitTheLoginTypeClaim_WhenCalled()
    {
        var claims = UserClaimsGenerator.Generate(UserFactory.GitHub());

        claims.Should().Contain(x => x.Type == CurrentUserService.Constants.LoginTypeClaimType && x.Value == UserClaimsGenerator.GitHubLoginType);
    }

    [Fact]
    public void Generate_ShouldEmitTheCreatedAtUnixSecondsClaim_WhenCalled()
    {
        var user = UserFactory.GitHub();

        var claims = UserClaimsGenerator.Generate(user);

        var value = claims.Single(x => x.Type == CurrentUserService.Constants.CreatedAtUnixTimeSecondsClaimType).Value;
        long.Parse(value, CultureInfo.InvariantCulture).Should().Be(user.CreationDate.ToUnixTimeSeconds());
    }

    [Fact]
    public void Generate_ShouldNotEmitARoleClaim_WhenCalled()
    {
        var claims = UserClaimsGenerator.Generate(UserFactory.GitHub());

        claims.Should().NotContain(x => x.Type == ClaimTypes.Role);
    }
}
