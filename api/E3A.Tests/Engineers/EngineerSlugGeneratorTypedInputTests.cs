using E3A.Domain.Engineers;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Engineers;

public sealed class EngineerSlugGeneratorTypedInputTests
{
    [Theory]
    [InlineData("  MMohsen ", "mmohsen")]
    [InlineData("DIVE-Backend", "dive-backend")]
    [InlineData("mmohsen", "mmohsen")]
    public void NormalizeTypedSlug_ShouldTrimAndLowercase_WhenInputHasCaseOrWhitespace(string slug, string expected)
    {
        EngineerSlugGenerator.NormalizeTypedSlug(slug).Should().Be(expected);
    }

    [Fact]
    public void NormalizeTypedSlug_ShouldReturnEmpty_WhenInputIsNull()
    {
        EngineerSlugGenerator.NormalizeTypedSlug(null).Should().BeEmpty();
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("a1")]
    [InlineData("dive-backend-engineer")]
    [InlineData("a-1-b")]
    public void IsValidFormat_ShouldReturnTrue_WhenSlugIsKebabCase(string slug)
    {
        EngineerSlugGenerator.IsValidFormat(slug).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("-abc")]
    [InlineData("abc-")]
    [InlineData("a--b")]
    [InlineData("Abc")]
    [InlineData("a_b")]
    [InlineData("a b")]
    [InlineData("abc!")]
    public void IsValidFormat_ShouldReturnFalse_WhenSlugIsNotKebabCase(string slug)
    {
        EngineerSlugGenerator.IsValidFormat(slug).Should().BeFalse();
    }
}
