using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using E3A.Application.Exceptions;
using E3A.Application.Teams.GetTeam;
using E3A.Domain.Teams;
using E3A.Tests.Teams.Shared;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace E3A.Tests.Teams.GetTeam;

public sealed class GetTeamQueryHandlerTests
{
    private readonly ITeamRepository _teamRepository = Substitute.For<ITeamRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _ownerUserId = Guid.NewGuid();
    private readonly GetTeamQueryHandler _sut;

    public GetTeamQueryHandlerTests()
    {
        _currentUserService.UserId.Returns(_ownerUserId);
        _sut = new GetTeamQueryHandler(_teamRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_ShouldReturnTeam_WhenTeamIsPublishedAndCallerIsAnonymous()
    {
        _currentUserService.UserId.Returns((Guid?)null);
        var team = TeamFactory.Published(Guid.NewGuid());
        StubTeam(team);

        var result = await _sut.Handle(new GetTeamQuery(team.Id), CancellationToken.None);

        result.Id.Should().Be(team.Id);
        result.Status.Should().Be(nameof(TeamStatus.Published));
    }

    [Fact]
    public async Task Handle_ShouldReturnTeam_WhenCallerIsTheOwnerOfADraft()
    {
        var team = TeamFactory.Draft(_ownerUserId);
        StubTeam(team);

        var result = await _sut.Handle(new GetTeamQuery(team.Id), CancellationToken.None);

        result.Id.Should().Be(team.Id);
        result.Status.Should().Be(nameof(TeamStatus.Draft));
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFound_WhenTeamDoesNotExist()
    {
        _teamRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Team>, IQueryable<Team>>>(), Arg.Any<bool>()).Returns((Team?)null);

        var act = async () => await _sut.Handle(new GetTeamQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundCoreException>().Where(x => x.ErrorCode == ErrorCodes.TeamNotFound);
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorized_WhenTeamIsDraftAndCallerIsAnonymous()
    {
        _currentUserService.UserId.Returns((Guid?)null);
        var team = TeamFactory.Draft(Guid.NewGuid());
        StubTeam(team);

        var act = async () => await _sut.Handle(new GetTeamQuery(team.Id), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedCoreException>().Where(x => x.ErrorCode == ErrorCodes.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_ShouldThrowForbidden_WhenTeamIsDraftAndCallerIsNotTheOwner()
    {
        var team = TeamFactory.Draft(Guid.NewGuid());
        StubTeam(team);

        var act = async () => await _sut.Handle(new GetTeamQuery(team.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenCoreException>().Where(x => x.ErrorCode == ErrorCodes.TeamNotOwned);
    }

    [Fact]
    public async Task Handle_ShouldOrderMembersBySortOrder_WhenTeamHasMembers()
    {
        var team = TeamFactory.WithMembers(_ownerUserId, TeamFactory.Pin("alpha"), TeamFactory.Pin("beta"), TeamFactory.Pin("gamma"));
        team.Members.Reverse();
        StubTeam(team);

        var result = await _sut.Handle(new GetTeamQuery(team.Id), CancellationToken.None);

        result.Members.Select(x => x.SortOrder).Should().Equal(0, 1, 2);
        result.Members.Select(x => x.EngineerSlug).Should().Equal("alpha", "beta", "gamma");
    }

    private void StubTeam(Team team)
        => _teamRepository.GetByIdAsync(team.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Team>, IQueryable<Team>>>(), Arg.Any<bool>()).Returns(team);
}
