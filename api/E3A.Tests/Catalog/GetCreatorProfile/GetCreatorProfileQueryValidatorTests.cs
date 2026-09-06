using E3A.Application.Catalog.GetCreatorProfile;
using E3A.Application.Exceptions;
using E3A.Application.Options;
using E3A.Tests.Authentication.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace E3A.Tests.Catalog.GetCreatorProfile;

public sealed class GetCreatorProfileQueryValidatorTests
{
    private readonly GitHubAuthenticationOptions _options = GitHubAuthenticationOptionsFactory.Default();
    private readonly GetCreatorProfileQueryValidator _sut;

    public GetCreatorProfileQueryValidatorTests()
    {
        _sut = new GetCreatorProfileQueryValidator(Options.Create(_options));
    }

    [Fact]
    public void Validate_ShouldPass_WhenLoginIsValid()
    {
        var result = _sut.Validate(new GetCreatorProfileQuery("octocat"));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ShouldFail_WhenLoginIsEmpty(string login)
    {
        var result = _sut.Validate(new GetCreatorProfileQuery(login));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.CatalogCreatorLoginRequired);
    }

    [Fact]
    public void Validate_ShouldFail_WhenLoginExceedsTheConfiguredMaxLength()
    {
        var result = _sut.Validate(new GetCreatorProfileQuery(new string('a', _options.GitHubLoginMaxLength + 1)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.CatalogCreatorLoginTooLong);
    }
}
