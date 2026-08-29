using Core.Identity.Tokens.AccessToken;
using Core.Utilities.Generator;
using Core.Identity.Tokens.CurrentUser;
using E3A.Application.Authentication.CompleteGitHubLogin;
using E3A.Application.Authentication.Shared;
using E3A.Domain.Identity;
using E3A.Tests.Authentication.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;
using System.Security.Claims;
using Xunit;

namespace E3A.Tests.Authentication.CompleteGitHubLogin;

public sealed class CompleteGitHubLoginHandlerTests
{
    private readonly IGitHubOAuthClient _gitHubOAuthClient = Substitute.For<IGitHubOAuthClient>();
    private readonly IOAuthStateProtector _oAuthStateProtector = Substitute.For<IOAuthStateProtector>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly IGenerator _generator = Substitute.For<IGenerator>();
    private readonly CompleteGitHubLoginHandler _sut;

    public CompleteGitHubLoginHandlerTests()
    {
        _oAuthStateProtector.Validate(Arg.Any<string?>(), Arg.Any<string?>()).Returns(OAuthStateStatus.Valid);
        _gitHubOAuthClient.ExchangeCodeForAccessTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("github-access-token");
        _gitHubOAuthClient.GetProfileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(GitHubProfileFactory.Default());
        _userRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<User>, IQueryable<User>>?>(), Arg.Any<Func<IQueryable<User>, IOrderedQueryable<User>>?>(), Arg.Any<bool>()).Returns((User?)null);
        _userRepository.IsUserNameExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _tokenService.GenerateTokenAsync(Arg.Any<List<Claim>>()).Returns("jwt-value");
        _sut = new CompleteGitHubLoginHandler(_gitHubOAuthClient, _oAuthStateProtector, _userRepository, _tokenService, _generator, Options.Create(GitHubAuthenticationOptionsFactory.Default()));
    }

    [Fact]
    public async Task Handle_ShouldCreateTheUser_WhenTheGitHubIdIsUnknown()
    {
        var profile = GitHubProfileFactory.Default();

        await _sut.Handle(new CompleteGitHubLoginCommand("github-code", "signed-state", "state-nonce"), CancellationToken.None);

        await _userRepository.Received(1).AddAsync(Arg.Is<User>(user => user.GitHubId == profile.Id && user.GitHubLogin == profile.Login && user.DisplayName == profile.Name && user.AvatarUrl == profile.AvatarUrl && user.UserName == profile.Login), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldCreateTheUserWithASuffixedUserName_WhenTheLoginIsAlreadyTaken()
    {
        var profile = GitHubProfileFactory.Default();
        _userRepository.IsUserNameExistsAsync(profile.Login.ToUpperInvariant(), Arg.Any<CancellationToken>()).Returns(true);
        _userRepository.IsUserNameExistsAsync("OCTOCAT-AB12", Arg.Any<CancellationToken>()).Returns(false);
        _generator.Generate(profile.Login, Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns("octocat-ab12-");

        await _sut.Handle(new CompleteGitHubLoginCommand("github-code", "signed-state", "state-nonce"), CancellationToken.None);

        await _userRepository.Received(1).AddAsync(Arg.Is<User>(user => user.UserName == "octocat-ab12" && user.GitHubLogin == profile.Login), Arg.Any<CancellationToken>());
        await _userRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnTheTokenInTheFragment_WhenLoginSucceeds()
    {
        var result = await _sut.Handle(new CompleteGitHubLoginCommand("github-code", "signed-state", "state-nonce"), CancellationToken.None);

        result.RedirectUrl.Should().Be($"{GitHubAuthenticationOptionsFactory.WebRedirectUrl}#token=jwt-value");
    }

    [Fact]
    public async Task Handle_ShouldSaveChangesOnce_WhenLoginSucceeds()
    {
        await _sut.Handle(new CompleteGitHubLoginCommand("github-code", "signed-state", "state-nonce"), CancellationToken.None);

        await _userRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldConsumeTheStateCookie_WhenLoginSucceeds()
    {
        var result = await _sut.Handle(new CompleteGitHubLoginCommand("github-code", "signed-state", "state-nonce"), CancellationToken.None);

        result.StateNonceConsumed.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldIssueTheTokenWithTheStoredUserId_WhenTheUserIsCreated()
    {
        User? createdUser = null;
        List<Claim>? issuedClaims = null;
        _userRepository.When(repository => repository.AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())).Do(call => createdUser = call.Arg<User>());
        _tokenService.GenerateTokenAsync(Arg.Do<List<Claim>>(claims => issuedClaims = claims)).Returns("jwt-value");

        await _sut.Handle(new CompleteGitHubLoginCommand("github-code", "signed-state", "state-nonce"), CancellationToken.None);

        var userIdClaim = issuedClaims!.Single(x => x.Type == CurrentUserService.Constants.UserIdClaimType);
        Guid.Parse(userIdClaim.Value).Should().Be(createdUser!.Id);
    }
}
