using Core.Identity.Tokens;
using Core.Utilities.Generator;
using E3A.Application.Authentication.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;
using System.Globalization;
using NSubstitute;
using Xunit;

namespace E3A.Tests.Authentication.Shared;

public sealed class OAuthStateProtectorTests
{
    private const string Nonce = "nonceonenonceon";
    private readonly IGenerator _generator = Substitute.For<IGenerator>();
    private readonly OAuthStateProtector _sut;

    public OAuthStateProtectorTests()
    {
        _generator.Generate(Arg.Any<int>(), Arg.Any<string>()).Returns(Nonce);
        _sut = new OAuthStateProtector(Options.Create(GitHubAuthenticationOptionsFactory.Default()), Options.Create(new JwtOptions { Key = "state-signing-key-that-is-long-enough" }), _generator);
    }

    [Fact]
    public void Create_ShouldProduceThreeDotSeparatedSegments_WhenCalled()
    {
        var state = _sut.Create();

        var segments = state.Split('.');
        segments.Should().HaveCount(3);
        segments[0].Should().Be(Nonce);
        long.TryParse(segments[1], NumberStyles.None, CultureInfo.InvariantCulture, out _).Should().BeTrue();
    }

    [Fact]
    public void Create_ShouldCarryTheConfiguredExpiry_WhenCalled()
    {
        var expected = DateTimeOffset.UtcNow.AddMinutes(GitHubAuthenticationOptionsFactory.Default().StateExpirationMinutes);

        var state = _sut.Create();

        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(long.Parse(state.Split('.')[1], CultureInfo.InvariantCulture));
        expiresAt.Should().BeCloseTo(expected, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Create_ShouldProduceDifferentStates_WhenCalledTwice()
    {
        _generator.Generate(Arg.Any<int>(), Arg.Any<string>()).Returns("nonceone", "noncetwo");

        var first = _sut.Create();
        var second = _sut.Create();

        first.Should().NotBe(second);
    }

    [Fact]
    public void Validate_ShouldReturnValid_WhenStateWasJustCreated()
    {
        var state = _sut.Create();

        var result = _sut.Validate(state);

        result.Should().Be(OAuthStateStatus.Valid);
    }

    [Fact]
    public void Validate_ShouldReturnExpired_WhenExpiryHasPassed()
    {
        var expiredProtector = new OAuthStateProtector(Options.Create(GitHubAuthenticationOptionsFactory.Default(stateExpirationMinutes: -1)), Options.Create(new JwtOptions { Key = "state-signing-key-that-is-long-enough" }), _generator);

        var result = expiredProtector.Validate(expiredProtector.Create());

        result.Should().Be(OAuthStateStatus.Expired);
    }

    [Fact]
    public void Validate_ShouldReturnValid_WhenTheSameStateIsValidatedTwice()
    {
        var state = _sut.Create();

        var first = _sut.Validate(state);
        var second = _sut.Validate(state);

        first.Should().Be(OAuthStateStatus.Valid);
        second.Should().Be(OAuthStateStatus.Valid);
    }
}
