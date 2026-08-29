using System.Linq.Expressions;
using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using E3A.Application.Exceptions;
using E3A.Application.Teams.ListMyTeams;
using E3A.Domain.Teams;
using E3A.Tests.Teams.Shared;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace E3A.Tests.Teams.ListMyTeams;

public sealed class ListMyTeamsQueryHandlerTests
{
    private readonly ITeamRepository _teamRepository = Substitute.For<ITeamRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _ownerUserId = Guid.NewGuid();
    private readonly ListMyTeamsQueryHandler _sut;

    public ListMyTeamsQueryHandlerTests()
    {
        _currentUserService.UserId.Returns(_ownerUserId);
        _sut = new ListMyTeamsQueryHandler(_teamRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_ShouldReturnOwnedTeamsNewestFirst_WhenCallerIsAuthenticated()
    {
        var older = TeamFactory.Draft(_ownerUserId, slug: "older-squad", creationDate: DateTimeOffset.UtcNow.AddDays(-2));
        var newer = TeamFactory.WithMembers(_ownerUserId, TeamFactory.Pin("alpha"), TeamFactory.Pin("beta"));
        StubTeams([older, newer]);

        var result = await _sut.Handle(new ListMyTeamsQuery(), CancellationToken.None);

        result.Select(x => x.Slug).Should().Equal(TeamFactory.DefaultSlug, "older-squad");
        result[0].MemberCount.Should().Be(2);
        result[1].MemberCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyList_WhenCallerOwnsNoTeams()
    {
        StubTeams([]);

        var result = await _sut.Handle(new ListMyTeamsQuery(), CancellationToken.None);

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorized_WhenUserIsNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = async () => await _sut.Handle(new ListMyTeamsQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedCoreException>().Where(x => x.ErrorCode == ErrorCodes.UserNotAuthenticated);
    }

    private void StubTeams(List<Team> teams)
        => _teamRepository.FindAsync(Arg.Any<Expression<Func<Team, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Team>, IQueryable<Team>>>(), Arg.Any<Func<IQueryable<Team>, IOrderedQueryable<Team>>>(), Arg.Any<bool>()).Returns(teams);
}
