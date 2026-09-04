using Core.Identity.Tokens;
using Core.Utilities.Generator;
using E3A.Application.Authentication.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace E3A.Tests.Authentication.Shared;

public sealed class OAuthStateProtectorNonceTests
{
    private const string FirstBrowserNonce = "firstbrowsernonce";
    private const string SecondBrowserNonce = "secondbrowsernonce";
    private readonly IGenerator _generator = Substitute.For<IGenerator>();
    private readonly OAuthStateProtector _sut;

    public OAuthStateProtectorNonceTests()
    {
        _generator.Generate(Arg.Any<int>(), Arg.Any<string>()).Returns(FirstBrowserNonce, SecondBrowserNonce);
        _sut = new OAuthStateProtector(Options.Create(GitHubAuthenticationOptionsFactory.Default()), Options.Create(new JwtOptions { Key = "state-signing-key-that-is-long-enough" }), _generator);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ShouldReturnInvalid_WhenNonceIsMissing(string? nonce)
    {
        var state = _sut.Create();

        var result = _sut.Validate(state.Value, nonce);

        result.Should().Be(OAuthStateStatus.Invalid);
    }

    [Fact]
    public void Validate_ShouldReturnInvalid_WhenNonceDoesNotMatch()
    {
        var state = _sut.Create();

        var result = _sut.Validate(state.Value, "a-nonce-the-state-never-carried");

        result.Should().Be(OAuthStateStatus.Invalid);
    }

    [Fact]
    public void Validate_ShouldReturnValid_WhenNonceMatches()
    {
        var state = _sut.Create();

        var result = _sut.Validate(state.Value, state.Nonce);

        result.Should().Be(OAuthStateStatus.Valid);
    }

    [Fact]
    public void Validate_ShouldReturnInvalid_WhenTheStateIsPresentedByAnotherBrowser()
    {
        var firstBrowserState = _sut.Create();
        var secondBrowserState = _sut.Create();

        _sut.Validate(firstBrowserState.Value, secondBrowserState.Nonce).Should().Be(OAuthStateStatus.Invalid);
        _sut.Validate(firstBrowserState.Value, firstBrowserState.Nonce).Should().Be(OAuthStateStatus.Valid);
    }

    [Fact]
    public void Validate_ShouldReturnInvalid_WhenTheNonceDoesNotMatchAnExpiredState()
    {
        var expiredProtector = new OAuthStateProtector(Options.Create(GitHubAuthenticationOptionsFactory.Default(stateExpirationMinutes: -1)), Options.Create(new JwtOptions { Key = "state-signing-key-that-is-long-enough" }), _generator);

        var expiredState = expiredProtector.Create();

        var result = expiredProtector.Validate(expiredState.Value, SecondBrowserNonce);

        result.Should().Be(OAuthStateStatus.Invalid);
        result.Should().NotBe(OAuthStateStatus.Expired);
    }
}
