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
}
