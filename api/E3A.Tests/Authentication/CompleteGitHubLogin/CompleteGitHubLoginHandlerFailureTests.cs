using Core.Identity.Tokens.AccessToken;
using Core.Utilities.Generator;
using E3A.Application.Authentication.CompleteGitHubLogin;
using E3A.Application.Authentication.Shared;
using E3A.Application.Exceptions;
using E3A.Domain.Identity;
using E3A.Tests.Authentication.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Security.Claims;
using Xunit;

namespace E3A.Tests.Authentication.CompleteGitHubLogin;

public sealed class CompleteGitHubLoginHandlerFailureTests
{
    private const string WebRedirectUrl = GitHubAuthenticationOptionsFactory.WebRedirectUrl;
    private readonly IGitHubOAuthClient _gitHubOAuthClient = Substitute.For<IGitHubOAuthClient>();
    private readonly IOAuthStateProtector _oAuthStateProtector = Substitute.For<IOAuthStateProtector>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly IGenerator _generator = Substitute.For<IGenerator>();
    private readonly CompleteGitHubLoginHandler _sut;

    public CompleteGitHubLoginHandlerFailureTests()
    {
        _oAuthStateProtector.Validate(Arg.Any<string?>(), Arg.Any<string?>()).Returns(OAuthStateStatus.Valid);
        _gitHubOAuthClient.ExchangeCodeForAccessTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("github-access-token");
        _gitHubOAuthClient.GetProfileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(GitHubProfileFactory.Default());
        _sut = new CompleteGitHubLoginHandler(_gitHubOAuthClient, _oAuthStateProtector, _userRepository, _tokenService, _generator, Options.Create(GitHubAuthenticationOptionsFactory.Default()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_ShouldRedirectWithCodeMissing_WhenCodeIsAbsent(string? code)
    {
        var result = await _sut.Handle(new CompleteGitHubLoginCommand(code, "signed-state", "state-nonce"), CancellationToken.None);

        result.RedirectUrl.Should().Be($"{WebRedirectUrl}#error={ErrorCodes.AuthenticationCodeMissing}");
        await _userRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldRedirectWithStateInvalid_WhenStateIsInvalid()
    {
        _oAuthStateProtector.Validate(Arg.Any<string?>(), Arg.Any<string?>()).Returns(OAuthStateStatus.Invalid);

        var result = await _sut.Handle(new CompleteGitHubLoginCommand("github-code", "tampered", "state-nonce"), CancellationToken.None);

        result.RedirectUrl.Should().Be($"{WebRedirectUrl}#error={ErrorCodes.AuthenticationStateInvalid}");
    }

    [Fact]
    public async Task Handle_ShouldNotCallGitHub_WhenStateIsInvalid()
    {
        _oAuthStateProtector.Validate(Arg.Any<string?>(), Arg.Any<string?>()).Returns(OAuthStateStatus.Invalid);

        await _sut.Handle(new CompleteGitHubLoginCommand("github-code", "tampered", "state-nonce"), CancellationToken.None);

        await _gitHubOAuthClient.DidNotReceive().ExchangeCodeForAccessTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldRedirectWithStateInvalid_WhenTheBrowserNonceIsMissing()
    {
        _oAuthStateProtector.Validate(Arg.Any<string?>(), Arg.Is<string?>(nonce => string.IsNullOrWhiteSpace(nonce))).Returns(OAuthStateStatus.Invalid);

        var result = await _sut.Handle(new CompleteGitHubLoginCommand("github-code", "signed-state", null), CancellationToken.None);

        result.RedirectUrl.Should().Be($"{WebRedirectUrl}#error={ErrorCodes.AuthenticationStateInvalid}");
        await _gitHubOAuthClient.DidNotReceive().ExchangeCodeForAccessTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldRedirectWithStateExpired_WhenStateIsExpired()
    {
        _oAuthStateProtector.Validate(Arg.Any<string?>(), Arg.Any<string?>()).Returns(OAuthStateStatus.Expired);

        var result = await _sut.Handle(new CompleteGitHubLoginCommand("github-code", "old-state", "state-nonce"), CancellationToken.None);

        result.RedirectUrl.Should().Be($"{WebRedirectUrl}#error={ErrorCodes.AuthenticationStateExpired}");
    }

    [Fact]
    public async Task Handle_ShouldRedirectWithExchangeFailed_WhenNoAccessTokenIsReturned()
    {
        _gitHubOAuthClient.ExchangeCodeForAccessTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((string?)null);

        var result = await _sut.Handle(new CompleteGitHubLoginCommand("github-code", "signed-state", "state-nonce"), CancellationToken.None);

        result.RedirectUrl.Should().Be($"{WebRedirectUrl}#error={ErrorCodes.GitHubTokenExchangeFailed}");
        await _gitHubOAuthClient.DidNotReceive().GetProfileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _userRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldRedirectWithProfileFetchFailed_WhenTheProfileIsNull()
    {
        _gitHubOAuthClient.GetProfileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((GitHubProfile?)null);

        var result = await _sut.Handle(new CompleteGitHubLoginCommand("github-code", "signed-state", "state-nonce"), CancellationToken.None);

        result.RedirectUrl.Should().Be($"{WebRedirectUrl}#error={ErrorCodes.GitHubProfileFetchFailed}");
        _tokenService.DidNotReceive().GenerateTokenAsync(Arg.Any<List<Claim>>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Handle_ShouldRedirectWithProfileInvalid_WhenTheProfileIdIsNotPositive(long id)
    {
        _gitHubOAuthClient.GetProfileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(GitHubProfileFactory.Default(id: id));

        var result = await _sut.Handle(new CompleteGitHubLoginCommand("github-code", "signed-state", "state-nonce"), CancellationToken.None);

        result.RedirectUrl.Should().Be($"{WebRedirectUrl}#error={ErrorCodes.GitHubProfileInvalid}");
        await _userRepository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _userRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_ShouldRedirectWithProfileInvalid_WhenTheLoginIsBlank(string login)
    {
        _gitHubOAuthClient.GetProfileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(GitHubProfileFactory.Default(login: login));

        var result = await _sut.Handle(new CompleteGitHubLoginCommand("github-code", "signed-state", "state-nonce"), CancellationToken.None);

        result.RedirectUrl.Should().Be($"{WebRedirectUrl}#error={ErrorCodes.GitHubProfileInvalid}");
        await _userRepository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldRedirectToTheConfiguredWebUrl_WhenAFailureOccurs()
    {
        _oAuthStateProtector.Validate(Arg.Any<string?>(), Arg.Any<string?>()).Returns(OAuthStateStatus.Invalid);

        var result = await _sut.Handle(new CompleteGitHubLoginCommand("github-code", "tampered", "state-nonce"), CancellationToken.None);

        result.RedirectUrl.Should().StartWith(WebRedirectUrl);
        result.RedirectUrl.Should().NotContain("github.com");
    }
}
