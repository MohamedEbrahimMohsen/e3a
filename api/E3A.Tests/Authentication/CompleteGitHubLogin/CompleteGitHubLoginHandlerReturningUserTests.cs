using Core.Identity.Tokens.AccessToken;
using E3A.Application.Authentication.CompleteGitHubLogin;
using E3A.Application.Authentication.Shared;
using E3A.Domain.Identity;
using E3A.Tests.Authentication.Shared;
using E3A.Tests.Identity.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;
using System.Security.Claims;
using Xunit;

namespace E3A.Tests.Authentication.CompleteGitHubLogin;

public sealed class CompleteGitHubLoginHandlerReturningUserTests
{
    private const string RenamedLogin = "octocat-renamed";
    private readonly IGitHubOAuthClient _gitHubOAuthClient = Substitute.For<IGitHubOAuthClient>();
    private readonly IOAuthStateProtector _oAuthStateProtector = Substitute.For<IOAuthStateProtector>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly User _storedUser = UserFactory.GitHub();
    private readonly CompleteGitHubLoginHandler _sut;

    public CompleteGitHubLoginHandlerReturningUserTests()
    {
        _oAuthStateProtector.Validate(Arg.Any<string?>()).Returns(OAuthStateStatus.Valid);
        _gitHubOAuthClient.ExchangeCodeForAccessTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("github-access-token");
        _gitHubOAuthClient.GetProfileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(GitHubProfileFactory.Default(login: RenamedLogin, name: "Renamed Octocat", avatarUrl: "https://avatars.githubusercontent.com/u/9999"));
        _userRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<User>, IQueryable<User>>?>(), Arg.Any<Func<IQueryable<User>, IOrderedQueryable<User>>?>(), Arg.Any<bool>())
            .Returns(call => call.Arg<Expression<Func<User, bool>>>().Compile()(_storedUser) ? _storedUser : null);
        _tokenService.GenerateTokenAsync(Arg.Any<List<Claim>>()).Returns("jwt-value");
        _sut = new CompleteGitHubLoginHandler(_gitHubOAuthClient, _oAuthStateProtector, _userRepository, _tokenService, Options.Create(GitHubAuthenticationOptionsFactory.Default()));
    }

    [Fact]
    public async Task Handle_ShouldMatchByGitHubNumericIdNotLogin_WhenTheLoginHasChanged()
    {
        await _sut.Handle(new CompleteGitHubLoginCommand("github-code", "signed-state"), CancellationToken.None);

        await _userRepository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldUpdateDisplayNameAndAvatar_WhenTheUserAlreadyExists()
    {
        await _sut.Handle(new CompleteGitHubLoginCommand("github-code", "signed-state"), CancellationToken.None);

        _storedUser.DisplayName.Should().Be("Renamed Octocat");
        _storedUser.AvatarUrl.Should().Be("https://avatars.githubusercontent.com/u/9999");
        _userRepository.Received(1).Update(_storedUser);
    }

    [Fact]
    public async Task Handle_ShouldNotChangeGitHubLoginOrUserName_WhenTheUserAlreadyExists()
    {
        await _sut.Handle(new CompleteGitHubLoginCommand("github-code", "signed-state"), CancellationToken.None);

        _storedUser.GitHubLogin.Should().Be(UserFactory.DefaultLogin);
        _storedUser.UserName.Should().Be(UserFactory.DefaultLogin);
    }

    [Fact]
    public async Task Handle_ShouldSaveChangesOnce_WhenTheUserAlreadyExists()
    {
        await _sut.Handle(new CompleteGitHubLoginCommand("github-code", "signed-state"), CancellationToken.None);

        await _userRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _userRepository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }
}
