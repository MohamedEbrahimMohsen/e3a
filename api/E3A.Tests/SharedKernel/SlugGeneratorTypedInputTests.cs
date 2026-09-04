using E3A.Domain.SharedKernel;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.SharedKernel;

public sealed class SlugGeneratorTypedInputTests
{
    [Theory]
    [InlineData("  MMohsen ", "mmohsen")]
    [InlineData("DIVE-Backend", "dive-backend")]
    [InlineData("mmohsen", "mmohsen")]
    public void NormalizeTypedSlug_ShouldTrimAndLowercase_WhenInputHasCaseOrWhitespace(string slug, string expected)
    {
        SlugGenerator.NormalizeTypedSlug(slug).Should().Be(expected);
    }

    [Fact]
    public void NormalizeTypedSlug_ShouldReturnEmpty_WhenInputIsNull()
    {
        SlugGenerator.NormalizeTypedSlug(null).Should().BeEmpty();
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("a1")]
    [InlineData("dive-backend-engineer")]
    [InlineData("a-1-b")]
    public void IsValidFormat_ShouldReturnTrue_WhenSlugIsKebabCase(string slug)
    {
        SlugGenerator.IsValidFormat(slug).Should().BeTrue();
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
        SlugGenerator.IsValidFormat(slug).Should().BeFalse();
    }
}
