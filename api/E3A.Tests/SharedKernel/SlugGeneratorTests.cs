using E3A.Domain.SharedKernel;
using E3A.Tests.Engineers.Shared;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.SharedKernel;

public sealed class SlugGeneratorTests
{
    private static readonly int SlugMaxLength = EngineerFactory.CreateEngineersOptions().SlugMaxLength;

    [Fact]
    public void Normalize_ShouldReturnKebabCaseSlug_WhenDisplayNameHasMixedCaseAndSpaces()
    {
        var slug = SlugGenerator.Normalize("Dive Backend Engineer", SlugMaxLength);

        slug.Should().Be("dive-backend-engineer");
    }

    [Theory]
    [InlineData("  .NET  DDD/CQRS Engineer! ", "net-ddd-cqrs-engineer")]
    [InlineData("--Hello--", "hello")]
    [InlineData("a@@@b", "a-b")]
    public void Normalize_ShouldCollapseAndTrimSeparators_WhenDisplayNameHasPunctuation(string displayName, string expectedSlug)
    {
        var slug = SlugGenerator.Normalize(displayName, SlugMaxLength);

        slug.Should().Be(expectedSlug);
    }

    [Fact]
    public void Normalize_ShouldDropNonAsciiCharacters_WhenDisplayNameIsNotEnglish()
    {
        var slug = SlugGenerator.Normalize("مهندس Backend", SlugMaxLength);

        slug.Should().Be("backend");
    }

    [Fact]
    public void Normalize_ShouldTruncateToMaxLength_WhenDisplayNameIsTooLong()
    {
        var slug = SlugGenerator.Normalize(new string('a', 150), SlugMaxLength);

        slug.Should().HaveLength(SlugMaxLength);
        slug.Should().NotEndWith("-");
    }
}
