using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Utilities.Generator;
using E3A.Application.Exceptions;
using E3A.Application.Teams.UpdateTeam;
using E3A.Domain.Teams;
using E3A.Tests.Teams.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace E3A.Tests.Teams.UpdateTeam;

public sealed class UpdateTeamHandlerTests
{
    private readonly ITeamRepository _teamRepository = Substitute.For<ITeamRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IGenerator _generator = Substitute.For<IGenerator>();
    private readonly Guid _ownerUserId = Guid.NewGuid();
    private readonly UpdateTeamHandler _sut;

    public UpdateTeamHandlerTests()
    {
        _currentUserService.UserId.Returns(_ownerUserId);
        _sut = new UpdateTeamHandler(_teamRepository, _currentUserService, _generator, Options.Create(TeamFactory.CreateTeamsOptions()));
    }

    [Fact]
    public async Task Handle_ShouldUpdateMetadata_WhenRequestIsValid()
    {
        var team = TeamFactory.Draft(_ownerUserId);
        _teamRepository.GetByIdAsync(team.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Team>, IQueryable<Team>>>(), Arg.Any<bool>()).Returns(team);

        var result = await _sut.Handle(new UpdateTeamCommand(team.Id, null, "Platform Squad", "A platform squad.", ["platform"]), CancellationToken.None);

        result.DisplayName.Should().Be("Platform Squad");
        result.Description.Should().Be("A platform squad.");
        result.Tags.Should().Equal("platform");
        _teamRepository.Received(1).Update(team);
        await _teamRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorized_WhenUserIsNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = async () => await _sut.Handle(new UpdateTeamCommand(Guid.NewGuid(), null, TeamFactory.DefaultDisplayName, null, []), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedCoreException>().Where(x => x.ErrorCode == ErrorCodes.UserNotAuthenticated);
        await _teamRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFound_WhenTeamDoesNotExist()
    {
        _teamRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Team>, IQueryable<Team>>>(), Arg.Any<bool>()).Returns((Team?)null);

        var act = async () => await _sut.Handle(new UpdateTeamCommand(Guid.NewGuid(), null, TeamFactory.DefaultDisplayName, null, []), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundCoreException>().Where(x => x.ErrorCode == ErrorCodes.TeamNotFound);
        await _teamRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowForbidden_WhenCallerIsNotTheOwner()
    {
        var team = TeamFactory.Draft(Guid.NewGuid());
        _teamRepository.GetByIdAsync(team.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Team>, IQueryable<Team>>>(), Arg.Any<bool>()).Returns(team);

        var act = async () => await _sut.Handle(new UpdateTeamCommand(team.Id, null, TeamFactory.DefaultDisplayName, null, []), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenCoreException>().Where(x => x.ErrorCode == ErrorCodes.TeamNotOwned);
        await _teamRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
