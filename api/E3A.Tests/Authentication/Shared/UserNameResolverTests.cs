using Core.Utilities.Generator;
using E3A.Application.Authentication.Shared;
using E3A.Domain.Identity;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace E3A.Tests.Authentication.Shared;

public sealed class UserNameResolverTests
{
    private const string Login = "octocat";
    private const string FirstCandidate = "octocat-ab12";
    private const string SecondCandidate = "octocat-cd34";

    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IGenerator _generator = Substitute.For<IGenerator>();

    [Fact]
    public async Task ResolveUniqueAsync_ShouldReturnTheGitHubLogin_WhenTheUserNameIsFree()
    {
        _userRepository.IsUserNameExistsAsync(Login.ToUpperInvariant(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await UserNameResolver.ResolveUniqueAsync(Login, _userRepository, _generator, GitHubAuthenticationOptionsFactory.Default(), CancellationToken.None);

        result.Should().Be(Login);
        _generator.DidNotReceive().Generate(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ResolveUniqueAsync_ShouldSuffixTheLogin_WhenTheUserNameIsHeldByAnotherRow()
    {
        _userRepository.IsUserNameExistsAsync(Login.ToUpperInvariant(), Arg.Any<CancellationToken>()).Returns(true);
        _userRepository.IsUserNameExistsAsync(FirstCandidate.ToUpperInvariant(), Arg.Any<CancellationToken>()).Returns(false);
        _generator.Generate(Login, Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns($"{FirstCandidate}-");

        var result = await UserNameResolver.ResolveUniqueAsync(Login, _userRepository, _generator, GitHubAuthenticationOptionsFactory.Default(), CancellationToken.None);

        result.Should().Be(FirstCandidate);
    }

    [Fact]
    public async Task ResolveUniqueAsync_ShouldRetry_WhenTheFirstCandidateIsAlsoTaken()
    {
        _userRepository.IsUserNameExistsAsync(Login.ToUpperInvariant(), Arg.Any<CancellationToken>()).Returns(true);
        _userRepository.IsUserNameExistsAsync(FirstCandidate.ToUpperInvariant(), Arg.Any<CancellationToken>()).Returns(true);
        _userRepository.IsUserNameExistsAsync(SecondCandidate.ToUpperInvariant(), Arg.Any<CancellationToken>()).Returns(false);
        _generator.Generate(Login, Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns($"{FirstCandidate}-", $"{SecondCandidate}-");

        var result = await UserNameResolver.ResolveUniqueAsync(Login, _userRepository, _generator, GitHubAuthenticationOptionsFactory.Default(), CancellationToken.None);

        result.Should().Be(SecondCandidate);
        _generator.Received(2).Generate(Login, Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ResolveUniqueAsync_ShouldAskForTheNormalizedUserName_WhenCalled()
    {
        await UserNameResolver.ResolveUniqueAsync("OctoCat", _userRepository, _generator, GitHubAuthenticationOptionsFactory.Default(), CancellationToken.None);

        await _userRepository.Received(1).IsUserNameExistsAsync("OCTOCAT", Arg.Any<CancellationToken>());
    }
}
