using E3A.Domain.Teams;
using E3A.Tests.Teams.Shared;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Teams;

public sealed class TeamMemberTests
{
    [Fact]
    public void Create_ShouldCopyPinAndSortOrder_WhenCalled()
    {
        var teamId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var pin = TeamFactory.Pin("dive-backend-engineer", semanticVersion: "2.1.0");
        var before = DateTimeOffset.UtcNow;

        var member = TeamMember.Create(teamId, pin, 3, createdBy);

        member.TeamId.Should().Be(teamId);
        member.EngineerId.Should().Be(pin.EngineerId);
        member.EngineerSlug.Should().Be("dive-backend-engineer");
        member.PinnedVersionId.Should().Be(pin.PinnedVersionId);
        member.PinnedSemanticVersion.Should().Be("2.1.0");
        member.SortOrder.Should().Be(3);
        member.CreatedBy.Should().Be(createdBy);
        member.CreationDate.Should().BeOnOrAfter(before);
        member.UpdationDate.Should().BeOnOrAfter(before);
    }
}
