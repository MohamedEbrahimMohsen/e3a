using E3A.Application.Publishing.Shared;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Publishing.Shared;

public sealed class PublicCatalogUrlTests
{
    [Fact]
    public void ForEngineer_ShouldBuildEngineerPageUrl_WhenCalled()
        => PublicCatalogUrl.ForEngineer("https://e3a.dev", "x").Should().Be("https://e3a.dev/e/x");

    [Fact]
    public void ForTeam_ShouldBuildTeamPageUrl_WhenCalled()
        => PublicCatalogUrl.ForTeam("https://e3a.dev", "x").Should().Be("https://e3a.dev/t/x");

    [Fact]
    public void ForTeam_ShouldNotDoubleSlash_WhenSiteUrlHasATrailingSlash()
        => PublicCatalogUrl.ForTeam("https://e3a.dev/", "x").Should().Be("https://e3a.dev/t/x");
}
