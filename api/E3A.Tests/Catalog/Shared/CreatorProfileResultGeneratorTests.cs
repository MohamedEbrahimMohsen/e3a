using E3A.Application.Catalog.Shared;
using E3A.Tests.Engineers.Shared;
using E3A.Tests.Identity.Shared;
using E3A.Tests.Teams.Shared;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Catalog.Shared;

public sealed class CreatorProfileResultGeneratorTests
{
    [Fact]
    public void Generate_ShouldMapTheCreatorFields_WhenCalled()
    {
        var creator = UserFactory.GitHub();

        var result = CreatorProfileResultGenerator.Generate(creator, [], []);

        result.Id.Should().Be(creator.Id);
        result.GitHubLogin.Should().Be(UserFactory.DefaultLogin);
        result.DisplayName.Should().Be(UserFactory.DefaultDisplayName);
        result.AvatarUrl.Should().Be(UserFactory.DefaultAvatarUrl);
        result.CreatedAt.Should().Be(creator.CreationDate);
    }

    [Fact]
    public void Generate_ShouldSumTheInstallCountsOfEveryEngineer_WhenCalled()
    {
        var creator = UserFactory.GitHub();
        var firstEngineer = EngineerFactory.Published(creator.Id, installCount: 3);
        var secondEngineer = EngineerFactory.Published(creator.Id, slug: "react-engineer", installCount: 4);

        var result = CreatorProfileResultGenerator.Generate(creator, [firstEngineer, secondEngineer], []);

        result.TotalInstalls.Should().Be(7);
    }

    [Fact]
    public void Generate_ShouldReturnZeroTotalInstalls_WhenTheCreatorHasNoEngineers()
    {
        var creator = UserFactory.GitHub();
        var team = TeamFactory.Published(creator.Id);

        var result = CreatorProfileResultGenerator.Generate(creator, [], [team]);

        result.Engineers.Should().BeEmpty();
        result.Teams.Should().ContainSingle();
        result.TotalInstalls.Should().Be(0);
    }
}
