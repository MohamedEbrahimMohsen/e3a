using Core.Identity.Tokens;
using Core.Utilities.Generator;
using E3A.Application.Authentication.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Globalization;
using Xunit;

namespace E3A.Tests.Authentication.Shared;

public sealed class OAuthStateProtectorTamperTests
{
    private const string SigningKey = "state-signing-key-that-is-long-enough";
    private readonly IGenerator _generator = Substitute.For<IGenerator>();
    private readonly OAuthStateProtector _sut;

    public OAuthStateProtectorTamperTests()
    {
        _generator.Generate(Arg.Any<int>(), Arg.Any<string>()).Returns("nonceonenonceon");
        _sut = Build(SigningKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ShouldReturnInvalid_WhenStateIsMissing(string? state)
    {
        var result = _sut.Validate(state);

        result.Should().Be(OAuthStateStatus.Invalid);
    }

    [Theory]
    [InlineData("nonceonly")]
    [InlineData("nonce.123456")]
    [InlineData("nonce.123456.signature.extra")]
    public void Validate_ShouldReturnInvalid_WhenSegmentCountIsWrong(string state)
    {
        var result = _sut.Validate(state);

        result.Should().Be(OAuthStateStatus.Invalid);
    }

    [Fact]
    public void Validate_ShouldReturnInvalid_WhenExpiryIsNotANumber()
    {
        var segments = _sut.Create().Split('.');

        var result = _sut.Validate($"{segments[0]}.later.{segments[2]}");

        result.Should().Be(OAuthStateStatus.Invalid);
    }

    [Fact]
    public void Validate_ShouldReturnInvalid_WhenNonceIsTampered()
    {
        var segments = _sut.Create().Split('.');

        var result = _sut.Validate($"tampered.{segments[1]}.{segments[2]}");

        result.Should().Be(OAuthStateStatus.Invalid);
    }

    [Fact]
    public void Validate_ShouldReturnInvalid_WhenExpiryIsExtendedWithoutResigning()
    {
        var segments = _sut.Create().Split('.');
        var extendedExpiry = DateTimeOffset.UtcNow.AddYears(1).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

        var result = _sut.Validate($"{segments[0]}.{extendedExpiry}.{segments[2]}");

        result.Should().Be(OAuthStateStatus.Invalid);
    }

    [Fact]
    public void Validate_ShouldReturnInvalid_WhenExpiryIsMovedIntoThePastWithoutResigning()
    {
        var segments = _sut.Create().Split('.');
        var pastExpiry = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

        var result = _sut.Validate($"{segments[0]}.{pastExpiry}.{segments[2]}");

        result.Should().Be(OAuthStateStatus.Invalid);
        result.Should().NotBe(OAuthStateStatus.Expired);
    }

    [Fact]
    public void Validate_ShouldReturnInvalid_WhenSignatureIsTampered()
    {
        var segments = _sut.Create().Split('.');

        var result = _sut.Validate($"{segments[0]}.{segments[1]}.{new string('A', segments[2].Length)}");

        result.Should().Be(OAuthStateStatus.Invalid);
    }

    [Fact]
    public void Validate_ShouldReturnInvalid_WhenSignatureIsTruncated()
    {
        var segments = _sut.Create().Split('.');

        var result = _sut.Validate($"{segments[0]}.{segments[1]}.{segments[2][..(segments[2].Length / 2)]}");

        result.Should().Be(OAuthStateStatus.Invalid);
    }

    [Fact]
    public void Validate_ShouldReturnInvalid_WhenStateWasSignedWithADifferentKey()
    {
        var foreignState = Build("a-completely-different-signing-key-value").Create();

        var result = _sut.Validate(foreignState);

        result.Should().Be(OAuthStateStatus.Invalid);
    }

    private OAuthStateProtector Build(string signingKey)
    {
        return new OAuthStateProtector(Options.Create(GitHubAuthenticationOptionsFactory.Default()), Options.Create(new JwtOptions { Key = signingKey }), _generator);
    }
}
