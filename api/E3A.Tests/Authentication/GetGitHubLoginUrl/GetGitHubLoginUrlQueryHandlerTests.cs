using E3A.Application.Authentication.GetGitHubLoginUrl;
using E3A.Application.Authentication.Shared;
using E3A.Tests.Authentication.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace E3A.Tests.Authentication.GetGitHubLoginUrl;

public sealed class GetGitHubLoginUrlQueryHandlerTests
{
    private readonly IOAuthStateProtector _oAuthStateProtector = Substitute.For<IOAuthStateProtector>();
    private readonly GetGitHubLoginUrlQueryHandler _sut;

    public GetGitHubLoginUrlQueryHandlerTests()
    {
        _oAuthStateProtector.Create().Returns(new OAuthState("signed-state", "state-nonce"));
        _sut = new GetGitHubLoginUrlQueryHandler(_oAuthStateProtector, Options.Create(GitHubAuthenticationOptionsFactory.Default()));
    }

    [Fact]
    public async Task Handle_ShouldRedirectToTheConfiguredAuthorizationUrl_WhenCalled()
    {
        var result = await _sut.Handle(new GetGitHubLoginUrlQuery(), CancellationToken.None);

        result.RedirectUrl.Should().StartWith(GitHubAuthenticationOptionsFactory.AuthorizationUrl);
    }

    [Fact]
    public async Task Handle_ShouldCarryTheStateFromTheProtector_WhenCalled()
    {
        var result = await _sut.Handle(new GetGitHubLoginUrlQuery(), CancellationToken.None);

        result.RedirectUrl.Should().Contain("state=signed-state");
    }

    [Fact]
    public async Task Handle_ShouldSurfaceTheNonceForTheBrowserCookie_WhenCalled()
    {
        var result = await _sut.Handle(new GetGitHubLoginUrlQuery(), CancellationToken.None);

        result.StateNonce.Should().Be("state-nonce");
        result.RedirectUrl.Should().NotContain("state-nonce");
    }

    [Fact]
    public async Task Handle_ShouldRequestANewState_WhenCalledTwice()
    {
        await _sut.Handle(new GetGitHubLoginUrlQuery(), CancellationToken.None);
        await _sut.Handle(new GetGitHubLoginUrlQuery(), CancellationToken.None);

        _oAuthStateProtector.Received(2).Create();
    }
}
