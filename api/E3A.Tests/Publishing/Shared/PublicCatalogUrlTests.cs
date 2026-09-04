using E3A.Application.Publishing.Shared;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Publishing.Shared;

public sealed class PublicCatalogUrlTests
{
    private const string SiteUrl = PublishingOptionsFactory.PublicSiteUrl;

    [Fact]
    public void ForEngineer_ShouldBuildEngineerPageUrl_WhenCalled()
        => PublicCatalogUrl.ForEngineer(SiteUrl, "x").Should().Be($"{SiteUrl}/e/x");

    [Fact]
    public void ForTeam_ShouldBuildTeamPageUrl_WhenCalled()
        => PublicCatalogUrl.ForTeam(SiteUrl, "x").Should().Be($"{SiteUrl}/t/x");

    [Fact]
    public void ForTeam_ShouldNotDoubleSlash_WhenSiteUrlHasATrailingSlash()
        => PublicCatalogUrl.ForTeam($"{SiteUrl}/", "x").Should().Be($"{SiteUrl}/t/x");
}
