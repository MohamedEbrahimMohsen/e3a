using E3A.Application.Catalog.Shared;
using E3A.Tests.Teams.Shared;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Catalog.Shared;

public sealed class CatalogTeamResultGeneratorTests
{
    [Fact]
    public void Generate_ShouldMapTheTeamFields_WhenCalled()
    {
        var team = TeamFactory.Published(Guid.NewGuid());

        var result = CatalogTeamResultGenerator.Generate(team);

        result.Should().BeEquivalentTo(new { team.Id, team.Slug, team.DisplayName, team.Description, team.Tags, team.LatestVersionId, CreatedAt = team.CreationDate, UpdatedAt = team.UpdationDate });
    }

    [Fact]
    public void Generate_ShouldOrderMemberSlugsBySortOrder_WhenTheTeamHasMembers()
    {
        var team = TeamFactory.WithMembers(Guid.NewGuid(), TeamFactory.Pin("b-engineer"), TeamFactory.Pin("a-engineer"));

        var result = CatalogTeamResultGenerator.Generate(team);

        result.MemberSlugs.Should().Equal("b-engineer", "a-engineer");
    }
}
