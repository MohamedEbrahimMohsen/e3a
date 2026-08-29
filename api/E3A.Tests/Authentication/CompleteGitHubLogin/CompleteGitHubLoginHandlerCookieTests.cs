using Core.Identity.Tokens.AccessToken;
using Core.Utilities.Generator;
using E3A.Application.Authentication.CompleteGitHubLogin;
using E3A.Application.Authentication.Shared;
using E3A.Domain.Identity;
using E3A.Tests.Authentication.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace E3A.Tests.Authentication.CompleteGitHubLogin;

public sealed class CompleteGitHubLoginHandlerCookieTests
{
    private readonly IGitHubOAuthClient _gitHubOAuthClient = Substitute.For<IGitHubOAuthClient>();
    private readonly IOAuthStateProtector _oAuthStateProtector = Substitute.For<IOAuthStateProtector>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly IGenerator _generator = Substitute.For<IGenerator>();
    private readonly CompleteGitHubLoginHandler _sut;

    public CompleteGitHubLoginHandlerCookieTests()
    {
        _oAuthStateProtector.Validate(Arg.Any<string?>(), Arg.Any<string?>()).Returns(OAuthStateStatus.Valid);
        _gitHubOAuthClient.ExchangeCodeForAccessTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("github-access-token");
        _gitHubOAuthClient.GetProfileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(GitHubProfileFactory.Default());
        _sut = new CompleteGitHubLoginHandler(_gitHubOAuthClient, _oAuthStateProtector, _userRepository, _tokenService, _generator, Options.Create(GitHubAuthenticationOptionsFactory.Default()));
    }

    [Fact]
    public async Task Handle_ShouldNotConsumeTheStateCookie_WhenCodeIsAbsent()
    {
        var result = await _sut.Handle(new CompleteGitHubLoginCommand(null, null, "state-nonce"), CancellationToken.None);

        result.StateNonceConsumed.Should().BeFalse();
        _oAuthStateProtector.DidNotReceive().Validate(Arg.Any<string?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task Handle_ShouldNotConsumeTheStateCookie_WhenTheBrowserNonceDoesNotMatch()
    {
        _oAuthStateProtector.Validate(Arg.Any<string?>(), Arg.Any<string?>()).Returns(OAuthStateStatus.Invalid);

        var result = await _sut.Handle(new CompleteGitHubLoginCommand("github-code", "signed-state", "another-nonce"), CancellationToken.None);

        result.StateNonceConsumed.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldConsumeTheStateCookie_WhenTheStateMatchesButTheExchangeFails()
    {
        _gitHubOAuthClient.ExchangeCodeForAccessTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((string?)null);

        var result = await _sut.Handle(new CompleteGitHubLoginCommand("github-code", "signed-state", "state-nonce"), CancellationToken.None);

        result.StateNonceConsumed.Should().BeTrue();
    }
}
