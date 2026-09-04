using System.Linq.Expressions;
using Core.DDD.Models;
using Core.Errors;
using E3A.Application.Exceptions;
using E3A.Application.Publishing.Shared;
using E3A.Domain.Identity;
using E3A.Domain.Publishing;
using E3A.Domain.Teams;
using E3A.Tests.Teams.Shared;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace E3A.Tests.Publishing.Shared;

public sealed class PublishedTeamCollectorTests
{
    private readonly ITeamRepository _teamRepository = Substitute.For<ITeamRepository>();
    private readonly IItemVersionRepository _itemVersionRepository = Substitute.For<IItemVersionRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly Guid _ownerUserId = Guid.NewGuid();
    private readonly List<ItemVersion> _publishedVersions = [];

    public PublishedTeamCollectorTests()
    {
        _itemVersionRepository.FindAsync(Arg.Any<Expression<Func<ItemVersion, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<ItemVersion>, IQueryable<ItemVersion>>?>(), Arg.Any<Func<IQueryable<ItemVersion>, IOrderedQueryable<ItemVersion>>?>(), Arg.Any<bool>()).Returns(_ => _publishedVersions);
        _userRepository.FindAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<User>, IQueryable<User>>?>(), Arg.Any<Func<IQueryable<User>, IOrderedQueryable<User>>?>(), Arg.Any<bool>()).Returns([]);
    }

    [Fact]
    public async Task CollectAsync_ShouldReturnPluginEntriesForPublishedTeams_WhenTeamsExist()
    {
        GivenPage(1, 1, PublishedTeam("alpha-squad"), PublishedTeam("beta-squad"));

        var plugins = await CollectAsync();

        plugins.Select(x => x.Name).Should().Equal("e3a-team-alpha-squad", "e3a-team-beta-squad");
        plugins[0].Version.Should().Be(ItemVersionFactory.DefaultSemanticVersion);
        plugins[0].Source.Sha256.Should().Be(ItemVersionFactory.DefaultZipSha256);
        plugins[0].Author.Url.Should().EndWith("/t/alpha-squad");
    }

    [Fact]
    public async Task CollectAsync_ShouldSkipTeams_WhenTheirLatestVersionIsNotPublished()
    {
        var unpublished = TeamFactory.Draft(_ownerUserId, slug: "ghost-squad");
        unpublished.MarkPublished(Guid.NewGuid());
        GivenPage(1, 1, PublishedTeam("alpha-squad"), unpublished);

        var plugins = await CollectAsync();

        plugins.Select(x => x.Name).Should().Equal("e3a-team-alpha-squad");
    }

    [Fact]
    public async Task CollectAsync_ShouldFallBackToTeamSlugForAuthorName_WhenOwnerUserNameIsBlank()
    {
        GivenPage(1, 1, PublishedTeam("alpha-squad"));

        var plugins = await CollectAsync();

        plugins[0].Author.Name.Should().Be("alpha-squad");
    }

    [Fact]
    public async Task CollectAsync_ShouldThrowInternalServerError_WhenTeamPagesExceedTheMaximum()
    {
        GivenPage(1, 5, PublishedTeam("alpha-squad"));
        GivenPage(2, 5, PublishedTeam("beta-squad"));

        var act = async () => await PublishedTeamCollector.CollectAsync(_teamRepository, _itemVersionRepository, _userRepository, PublishingOptionsFactory.Default(marketplaceMaxPages: 1), CancellationToken.None);

        await act.Should().ThrowAsync<InternalServerErrorCoreException>().Where(x => x.ErrorCode == ErrorCodes.MarketplaceTeamLimitExceeded);
    }

    private Task<List<MarketplacePlugin>> CollectAsync()
        => PublishedTeamCollector.CollectAsync(_teamRepository, _itemVersionRepository, _userRepository, PublishingOptionsFactory.Default(), CancellationToken.None);

    private Team PublishedTeam(string slug)
    {
        var team = TeamFactory.Draft(_ownerUserId, slug: slug);
        var version = ItemVersionFactory.Published(team.Id);
        team.MarkPublished(version.Id);
        _publishedVersions.Add(version);

        return team;
    }

    private void GivenPage(int pageNumber, int totalPages, params Team[] teams)
        => _teamRepository.FindPaginatedAsync(pageNumber, Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Team, bool>>>(), Arg.Any<Func<IQueryable<Team>, IQueryable<Team>>>(), Arg.Any<Func<IQueryable<Team>, IOrderedQueryable<Team>>>(), Arg.Any<bool>()).Returns(new PageData<Team> { Items = [.. teams], TotalPages = totalPages });
}
