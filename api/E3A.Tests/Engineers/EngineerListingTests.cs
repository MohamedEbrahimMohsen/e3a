using E3A.Domain.Engineers;
using E3A.Tests.Engineers.Shared;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Engineers;

public sealed class EngineerListingTests
{
    [Fact]
    public void Unlist_ShouldSetUnlistedAndAdvanceUpdationDate_WhenCalled()
    {
        var engineer = EngineerFactory.Published(Guid.NewGuid());
        var latestVersionId = engineer.LatestVersionId;
        var before = DateTimeOffset.UtcNow;

        engineer.Unlist();

        engineer.Status.Should().Be(EngineerStatus.Unlisted);
        engineer.LatestVersionId.Should().Be(latestVersionId);
        engineer.UpdationDate.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void Relist_ShouldSetPublishedAndAdvanceUpdationDate_WhenCalled()
    {
        var engineer = EngineerFactory.Unlisted(Guid.NewGuid());
        var before = DateTimeOffset.UtcNow;

        engineer.Relist();

        engineer.Status.Should().Be(EngineerStatus.Published);
        engineer.UpdationDate.Should().BeOnOrAfter(before);
    }
}
