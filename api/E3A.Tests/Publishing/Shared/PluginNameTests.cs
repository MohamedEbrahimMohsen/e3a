using E3A.Application.Publishing.Shared;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Publishing.Shared;

public sealed class PluginNameTests
{
    [Fact]
    public void ForEngineer_ShouldPrefixWithE3a_WhenCalled()
        => PluginName.ForEngineer("x").Should().Be("e3a-x");

    [Fact]
    public void ForTeam_ShouldPrefixWithE3aTeam_WhenCalled()
        => PluginName.ForTeam("x").Should().Be("e3a-team-x");

    [Theory]
    [InlineData("team-alpha")]
    [InlineData("Team-Alpha")]
    [InlineData("team-")]
    public void IsTeamNamespaced_ShouldReturnTrue_WhenSlugStartsWithTheTeamSegment(string slug)
        => PluginName.IsTeamNamespaced(slug).Should().BeTrue();

    [Theory]
    [InlineData("alpha")]
    [InlineData("teams")]
    [InlineData("team")]
    [InlineData("steam-alpha")]
    public void IsTeamNamespaced_ShouldReturnFalse_WhenSlugDoesNotStartWithTheTeamSegment(string slug)
        => PluginName.IsTeamNamespaced(slug).Should().BeFalse();
}
