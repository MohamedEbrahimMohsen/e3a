using E3A.Domain.Engineers;
using E3A.Tests.Engineers.Shared;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Engineers;

public sealed class EngineerSlugGeneratorTests
{
    private static readonly int SlugMaxLength = EngineerFactory.CreateEngineersOptions().SlugMaxLength;

    [Fact]
    public void Normalize_ShouldReturnKebabCaseSlug_WhenDisplayNameHasMixedCaseAndSpaces()
    {
        var slug = EngineerSlugGenerator.Normalize("Dive Backend Engineer", SlugMaxLength);

        slug.Should().Be("dive-backend-engineer");
    }

    [Theory]
    [InlineData("  .NET  DDD/CQRS Engineer! ", "net-ddd-cqrs-engineer")]
    [InlineData("--Hello--", "hello")]
    [InlineData("a@@@b", "a-b")]
    public void Normalize_ShouldCollapseAndTrimSeparators_WhenDisplayNameHasPunctuation(string displayName, string expectedSlug)
    {
        var slug = EngineerSlugGenerator.Normalize(displayName, SlugMaxLength);

        slug.Should().Be(expectedSlug);
    }

    [Fact]
    public void Normalize_ShouldDropNonAsciiCharacters_WhenDisplayNameIsNotEnglish()
    {
        var slug = EngineerSlugGenerator.Normalize("مهندس Backend", SlugMaxLength);

        slug.Should().Be("backend");
    }

    [Fact]
    public void Normalize_ShouldTruncateToMaxLength_WhenDisplayNameIsTooLong()
    {
        var slug = EngineerSlugGenerator.Normalize(new string('a', 150), SlugMaxLength);

        slug.Should().HaveLength(SlugMaxLength);
        slug.Should().NotEndWith("-");
    }
}
