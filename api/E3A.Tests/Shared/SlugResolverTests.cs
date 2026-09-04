using Core.Utilities.Generator;
using E3A.Application.Shared;
using E3A.Domain.Engineers;
using E3A.Domain.SharedKernel;
using E3A.Tests.Engineers.Shared;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace E3A.Tests.Shared;

public sealed class SlugResolverTests
{
    private const string BaseSlug = "mmohsen";
    private const string FirstCandidate = "mmohsen-ab12";
    private const string SecondCandidate = "mmohsen-cd34";

    private static readonly int SlugMaxLength = EngineerFactory.CreateEngineersOptions().SlugMaxLength;
    private static readonly int SlugSuffixSize = EngineerFactory.CreateEngineersOptions().SlugSuffixSize;

    private readonly IEngineerRepository _engineerRepository = Substitute.For<IEngineerRepository>();
    private readonly IGenerator _generator = Substitute.For<IGenerator>();

    [Fact]
    public async Task ResolveUniqueAsync_ShouldReturnBaseSlug_WhenBaseSlugIsFree()
    {
        _engineerRepository.IsSlugExistsAsync(BaseSlug, Arg.Any<CancellationToken>()).Returns(false);

        var result = await SlugResolver.ResolveUniqueAsync(BaseSlug, _engineerRepository.IsSlugExistsAsync, _generator, SlugMaxLength, SlugSuffixSize, CancellationToken.None);

        result.Should().Be(BaseSlug);
        _generator.DidNotReceive().Generate(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ResolveUniqueAsync_ShouldStripTrailingSeparator_WhenGeneratorAppendsOne()
    {
        _engineerRepository.IsSlugExistsAsync(BaseSlug, Arg.Any<CancellationToken>()).Returns(true);
        _engineerRepository.IsSlugExistsAsync(FirstCandidate, Arg.Any<CancellationToken>()).Returns(false);
        _generator.Generate(BaseSlug, Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns($"{FirstCandidate}-");

        var result = await SlugResolver.ResolveUniqueAsync(BaseSlug, _engineerRepository.IsSlugExistsAsync, _generator, SlugMaxLength, SlugSuffixSize, CancellationToken.None);

        result.Should().Be(FirstCandidate);
        SlugGenerator.IsValidFormat(result).Should().BeTrue();
    }

    [Fact]
    public async Task ResolveUniqueAsync_ShouldRetry_WhenFirstCandidateIsAlsoTaken()
    {
        _engineerRepository.IsSlugExistsAsync(BaseSlug, Arg.Any<CancellationToken>()).Returns(true);
        _engineerRepository.IsSlugExistsAsync(FirstCandidate, Arg.Any<CancellationToken>()).Returns(true);
        _engineerRepository.IsSlugExistsAsync(SecondCandidate, Arg.Any<CancellationToken>()).Returns(false);
        _generator.Generate(BaseSlug, Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns($"{FirstCandidate}-", $"{SecondCandidate}-");

        var result = await SlugResolver.ResolveUniqueAsync(BaseSlug, _engineerRepository.IsSlugExistsAsync, _generator, SlugMaxLength, SlugSuffixSize, CancellationToken.None);

        result.Should().Be(SecondCandidate);
        _generator.Received(2).Generate(BaseSlug, Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ResolveUniqueAsync_ShouldShortenPrefix_WhenBaseSlugIsAtMaxLength()
    {
        var longSlug = new string('a', 100);
        _engineerRepository.IsSlugExistsAsync(longSlug, Arg.Any<CancellationToken>()).Returns(true);
        _generator.Generate(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns($"{FirstCandidate}-");

        await SlugResolver.ResolveUniqueAsync(longSlug, _engineerRepository.IsSlugExistsAsync, _generator, SlugMaxLength, SlugSuffixSize, CancellationToken.None);

        _generator.Received(1).Generate(Arg.Is<string>(prefix => prefix.Length == 95), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }
}
