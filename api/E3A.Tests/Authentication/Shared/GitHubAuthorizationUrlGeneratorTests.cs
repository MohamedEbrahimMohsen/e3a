using E3A.Application.Authentication.Shared;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Authentication.Shared;

public sealed class GitHubAuthorizationUrlGeneratorTests
{
    private const string State = "nonceone.1900000000.signature";

    [Fact]
    public void Generate_ShouldStartWithTheConfiguredAuthorizationUrl_WhenCalled()
    {
        var options = GitHubAuthenticationOptionsFactory.Default();

        var url = GitHubAuthorizationUrlGenerator.Generate(options, State);

        url.Should().StartWith(options.AuthorizationUrl);
    }

    [Fact]
    public void Generate_ShouldCarryClientIdRedirectUriScopeAndState_WhenCalled()
    {
        var options = GitHubAuthenticationOptionsFactory.Default();

        var url = GitHubAuthorizationUrlGenerator.Generate(options, State);

        url.Should().Contain($"client_id={options.ClientId}");
        url.Should().Contain($"redirect_uri={Uri.EscapeDataString(options.CallbackUrl)}");
        url.Should().Contain($"scope={Uri.EscapeDataString(options.Scope)}");
        url.Should().Contain($"state={Uri.EscapeDataString(State)}");
    }

    [Fact]
    public void Generate_ShouldEscapeTheRedirectUri_WhenItContainsReservedCharacters()
    {
        var options = GitHubAuthenticationOptionsFactory.Default();

        var query = GitHubAuthorizationUrlGenerator.Generate(options, State)[options.AuthorizationUrl.Length..];

        query.Should().NotContain("://");
        query.Should().Contain("%3A%2F%2F");
    }
}
