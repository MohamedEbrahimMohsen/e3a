using E3A.Tests.Engineers.Shared;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Engineers;

public sealed class EngineerSlugTests
{
    [Fact]
    public void ChangeSlug_ShouldReplaceSlugAndStampUpdationDate_WhenCalled()
    {
        var engineer = EngineerFactory.Draft(Guid.NewGuid());
        var before = DateTimeOffset.UtcNow;

        engineer.ChangeSlug("mmohsen");

        engineer.Slug.Should().Be("mmohsen");
        engineer.UpdationDate.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void IsSlugMutable_ShouldBeTrue_WhenEngineerHasNoLatestVersion()
    {
        var engineer = EngineerFactory.Draft(Guid.NewGuid());

        engineer.IsSlugMutable.Should().BeTrue();
    }

    [Fact]
    public void IsSlugMutable_ShouldBeFalse_WhenEngineerIsPublished()
    {
        var engineer = EngineerFactory.Published(Guid.NewGuid());

        engineer.IsSlugMutable.Should().BeFalse();
    }
}
