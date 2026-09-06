using Core.Errors;
using E3A.Application.Catalog.GetCreatorProfile;
using E3A.Application.Exceptions;
using E3A.Domain.Engineers;
using E3A.Domain.Identity;
using E3A.Domain.Teams;
using E3A.Tests.Engineers.Shared;
using E3A.Tests.Identity.Shared;
using E3A.Tests.Teams.Shared;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;
using Xunit;

namespace E3A.Tests.Catalog.GetCreatorProfile;

public sealed class GetCreatorProfileQueryHandlerTests
{
    private static readonly DateTimeOffset OldestCreationDate = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IEngineerRepository _engineerRepository = Substitute.For<IEngineerRepository>();
    private readonly ITeamRepository _teamRepository = Substitute.For<ITeamRepository>();
    private readonly GetCreatorProfileQueryHandler _sut;

    public GetCreatorProfileQueryHandlerTests()
    {
        _sut = new GetCreatorProfileQueryHandler(_userRepository, _engineerRepository, _teamRepository);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFound_WhenNoUserMatchesTheLogin()
    {
        _userRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), asNoTracking: true).Returns((User?)null);

        var act = async () => await _sut.Handle(new GetCreatorProfileQuery("ghost-creator"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundCoreException>().Where(x => x.ErrorCode == ErrorCodes.UserNotFound);
        await _engineerRepository.DidNotReceive().FindAsync(Arg.Any<Expression<Func<Engineer, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Engineer>, IQueryable<Engineer>>>(), Arg.Any<Func<IQueryable<Engineer>, IOrderedQueryable<Engineer>>>(), Arg.Any<bool>());
        await _teamRepository.DidNotReceive().FindAsync(Arg.Any<Expression<Func<Team, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Team>, IQueryable<Team>>>(), Arg.Any<Func<IQueryable<Team>, IOrderedQueryable<Team>>>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task Handle_ShouldReturnTheCreatorHeader_WhenTheLoginMatches()
    {
        var creator = GivenCreator();

        var result = await _sut.Handle(new GetCreatorProfileQuery(UserFactory.DefaultLogin), CancellationToken.None);

        result.Should().BeEquivalentTo(new { creator.Id, GitHubLogin = UserFactory.DefaultLogin, DisplayName = UserFactory.DefaultDisplayName, AvatarUrl = UserFactory.DefaultAvatarUrl, CreatedAt = creator.CreationDate });
    }

    [Fact]
    public async Task Handle_ShouldReturnOnlyPublishedEngineers_WhenTheCreatorAlsoHasDraftAndUnlistedOnes()
    {
        var creator = GivenCreator();
        GivenEngineers(EngineerFactory.Published(creator.Id, installCount: 7), EngineerFactory.Draft(creator.Id, slug: "draft-engineer"), EngineerFactory.Unlisted(creator.Id, slug: "unlisted-engineer"));

        var result = await _sut.Handle(new GetCreatorProfileQuery(UserFactory.DefaultLogin), CancellationToken.None);

        result.Engineers.Should().ContainSingle().Which.Slug.Should().Be(EngineerFactory.DefaultSlug);
        result.TotalInstalls.Should().Be(7);
    }

    [Fact]
    public async Task Handle_ShouldReturnOnlyPublishedTeams_WhenTheCreatorAlsoHasDraftOnes()
    {
        var creator = GivenCreator();
        GivenTeams(TeamFactory.Published(creator.Id), TeamFactory.Draft(creator.Id, slug: "draft-team"));

        var result = await _sut.Handle(new GetCreatorProfileQuery(UserFactory.DefaultLogin), CancellationToken.None);

        result.Teams.Should().ContainSingle().Which.Slug.Should().Be(TeamFactory.DefaultSlug);
    }

    [Fact]
    public async Task Handle_ShouldOrderEngineersByNewestFirst_WhenSeveralArePublished()
    {
        var creator = GivenCreator();
        GivenEngineers(PublishedEngineer(creator.Id, "middle-engineer", 10), PublishedEngineer(creator.Id, "oldest-engineer", 0), PublishedEngineer(creator.Id, "newest-engineer", 20));

        var result = await _sut.Handle(new GetCreatorProfileQuery(UserFactory.DefaultLogin), CancellationToken.None);

        result.Engineers.Select(x => x.Slug).Should().Equal("newest-engineer", "middle-engineer", "oldest-engineer");
    }

    [Fact]
    public async Task Handle_ShouldOrderTeamsByNewestFirst_WhenSeveralArePublished()
    {
        var creator = GivenCreator();
        GivenTeams(PublishedTeam(creator.Id, "middle-team", 10), PublishedTeam(creator.Id, "oldest-team", 0), PublishedTeam(creator.Id, "newest-team", 20));

        var result = await _sut.Handle(new GetCreatorProfileQuery(UserFactory.DefaultLogin), CancellationToken.None);

        result.Teams.Select(x => x.Slug).Should().Equal("newest-team", "middle-team", "oldest-team");
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyLists_WhenTheCreatorHasPublishedNothing()
    {
        GivenCreator();

        var result = await _sut.Handle(new GetCreatorProfileQuery(UserFactory.DefaultLogin), CancellationToken.None);

        result.Engineers.Should().NotBeNull().And.BeEmpty();
        result.Teams.Should().NotBeNull().And.BeEmpty();
        result.TotalInstalls.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldLoadTeamMembers_WhenQueryingTeams()
    {
        GivenCreator();

        await _sut.Handle(new GetCreatorProfileQuery(UserFactory.DefaultLogin), CancellationToken.None);

        await _teamRepository.Received(1).FindAsync(Arg.Any<Expression<Func<Team, bool>>>(), Arg.Any<CancellationToken>(), include: Arg.Is<Func<IQueryable<Team>, IQueryable<Team>>>(x => x != null), orderBy: Arg.Any<Func<IQueryable<Team>, IOrderedQueryable<Team>>>(), asNoTracking: true);
    }

    private static Engineer PublishedEngineer(Guid ownerUserId, string slug, int dayOffset) => EngineerFactory.Published(ownerUserId, slug, creationDate: OldestCreationDate.AddDays(dayOffset));

    private static Team PublishedTeam(Guid ownerUserId, string slug, int dayOffset)
    {
        var team = TeamFactory.Published(ownerUserId, slug);
        team.CreationDate = OldestCreationDate.AddDays(dayOffset);

        return team;
    }

    private User GivenCreator()
    {
        var creator = UserFactory.GitHub();
        _userRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), asNoTracking: true).Returns(creator);
        GivenEngineers();
        GivenTeams();

        return creator;
    }

    private void GivenEngineers(params Engineer[] engineers) => _engineerRepository.FindAsync(Arg.Any<Expression<Func<Engineer, bool>>>(), Arg.Any<CancellationToken>(), asNoTracking: true).Returns([.. engineers]);

    private void GivenTeams(params Team[] teams) => _teamRepository.FindAsync(Arg.Any<Expression<Func<Team, bool>>>(), Arg.Any<CancellationToken>(), include: Arg.Any<Func<IQueryable<Team>, IQueryable<Team>>>(), asNoTracking: true).Returns([.. teams]);
}
