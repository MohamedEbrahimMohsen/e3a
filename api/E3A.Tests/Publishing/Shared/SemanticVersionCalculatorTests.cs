using E3A.Application.Publishing.Shared;
using E3A.Domain.Publishing;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Publishing.Shared;

public sealed class SemanticVersionCalculatorTests
{
    [Theory]
    [InlineData(null, VersionIncrement.Patch)]
    [InlineData(null, VersionIncrement.Minor)]
    [InlineData(null, VersionIncrement.Major)]
    [InlineData("", VersionIncrement.Patch)]
    [InlineData("", VersionIncrement.Minor)]
    [InlineData("", VersionIncrement.Major)]
    [InlineData("   ", VersionIncrement.Patch)]
    [InlineData("   ", VersionIncrement.Minor)]
    [InlineData("   ", VersionIncrement.Major)]
    [InlineData("not-a-version", VersionIncrement.Patch)]
    [InlineData("not-a-version", VersionIncrement.Minor)]
    [InlineData("not-a-version", VersionIncrement.Major)]
    [InlineData("1.2", VersionIncrement.Patch)]
    [InlineData("1.2", VersionIncrement.Minor)]
    [InlineData("1.2", VersionIncrement.Major)]
    public void Next_ShouldReturnInitialVersion_WhenPreviousIsMissing(string? previousSemanticVersion, VersionIncrement increment)
    {
        SemanticVersionCalculator.Next(previousSemanticVersion, increment).Should().Be("1.0.0");
    }

    [Theory]
    [InlineData("1.2.3", VersionIncrement.Patch, "1.2.4")]
    [InlineData("1.2.3", VersionIncrement.Minor, "1.3.0")]
    [InlineData("1.2.3", VersionIncrement.Major, "2.0.0")]
    [InlineData("0.0.9", VersionIncrement.Patch, "0.0.10")]
    public void Next_ShouldBumpCorrectComponent_WhenPreviousExists(string previousSemanticVersion, VersionIncrement increment, string expected)
    {
        SemanticVersionCalculator.Next(previousSemanticVersion, increment).Should().Be(expected);
    }
}
