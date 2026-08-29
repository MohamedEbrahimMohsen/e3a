using E3A.Application.Authentication.Shared;
using E3A.Application.Exceptions;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Authentication.Shared;

public sealed class AuthenticationRedirectUrlGeneratorTests
{
    private const string WebRedirectUrl = GitHubAuthenticationOptionsFactory.WebRedirectUrl;

    [Fact]
    public void Success_ShouldPlaceTheTokenInTheFragment_WhenCalled()
    {
        var url = AuthenticationRedirectUrlGenerator.Success(WebRedirectUrl, "jwt-value");

        url.Should().Be($"{WebRedirectUrl}#token=jwt-value");
        url.Should().NotContain("?");
    }

    [Fact]
    public void Success_ShouldEscapeTheToken_WhenItContainsReservedCharacters()
    {
        var url = AuthenticationRedirectUrlGenerator.Success(WebRedirectUrl, "jwt#value&more");

        url.Should().Be($"{WebRedirectUrl}#token=jwt%23value%26more");
    }

    [Fact]
    public void Failure_ShouldPlaceTheErrorCodeInTheFragment_WhenCalled()
    {
        var url = AuthenticationRedirectUrlGenerator.Failure(WebRedirectUrl, ErrorCodes.AuthenticationStateInvalid);

        url.Should().Be($"{WebRedirectUrl}#error={ErrorCodes.AuthenticationStateInvalid}");
    }

    [Fact]
    public void Failure_ShouldStartWithTheConfiguredRedirectUrl_WhenCalled()
    {
        var url = AuthenticationRedirectUrlGenerator.Failure(WebRedirectUrl, ErrorCodes.GitHubProfileInvalid);

        url.Should().StartWith(WebRedirectUrl);
    }
}
