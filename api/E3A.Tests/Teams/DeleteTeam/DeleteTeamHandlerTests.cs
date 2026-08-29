using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using E3A.Application.Exceptions;
using E3A.Application.Teams.DeleteTeam;
using E3A.Domain.Teams;
using E3A.Tests.Teams.Shared;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace E3A.Tests.Teams.DeleteTeam;

public sealed class DeleteTeamHandlerTests
{
    private readonly ITeamRepository _teamRepository = Substitute.For<ITeamRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _ownerUserId = Guid.NewGuid();
    private readonly DeleteTeamHandler _sut;

    public DeleteTeamHandlerTests()
    {
        _currentUserService.UserId.Returns(_ownerUserId);
        _sut = new DeleteTeamHandler(_teamRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_ShouldSoftDeleteTeam_WhenCallerIsTheOwner()
    {
        var team = TeamFactory.Draft(_ownerUserId);
        _teamRepository.GetByIdAsync(team.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Team>, IQueryable<Team>>>(), Arg.Any<bool>()).Returns(team);

        await _sut.Handle(new DeleteTeamCommand(team.Id), CancellationToken.None);

        team.Status.Should().Be(TeamStatus.Deleted);
        team.IsDeleted.Should().BeTrue();
        await _teamRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorized_WhenUserIsNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = async () => await _sut.Handle(new DeleteTeamCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedCoreException>().Where(x => x.ErrorCode == ErrorCodes.UserNotAuthenticated);
        await _teamRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFound_WhenTeamDoesNotExist()
    {
        _teamRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Team>, IQueryable<Team>>>(), Arg.Any<bool>()).Returns((Team?)null);

        var act = async () => await _sut.Handle(new DeleteTeamCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundCoreException>().Where(x => x.ErrorCode == ErrorCodes.TeamNotFound);
        await _teamRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowForbidden_WhenCallerIsNotTheOwner()
    {
        var team = TeamFactory.Draft(Guid.NewGuid());
        _teamRepository.GetByIdAsync(team.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Team>, IQueryable<Team>>>(), Arg.Any<bool>()).Returns(team);

        var act = async () => await _sut.Handle(new DeleteTeamCommand(team.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenCoreException>().Where(x => x.ErrorCode == ErrorCodes.TeamNotOwned);
        await _teamRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
