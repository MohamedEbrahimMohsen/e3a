using System.Linq.Expressions;
using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using E3A.Application.Exceptions;
using E3A.Application.Teams.PublishTeam;
using E3A.Domain.Publishing;
using E3A.Domain.Teams;
using E3A.Tests.Publishing.Shared;
using E3A.Tests.Teams.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace E3A.Tests.Teams.PublishTeam;

public sealed class PublishTeamHandlerGuardTests
{
    private readonly ITeamRepository _teamRepository = Substitute.For<ITeamRepository>();
    private readonly IItemVersionRepository _itemVersionRepository = Substitute.For<IItemVersionRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _ownerUserId = Guid.NewGuid();
    private readonly PublishTeamHandler _sut;

    public PublishTeamHandlerGuardTests()
    {
        _currentUserService.UserId.Returns(_ownerUserId);
        _sut = new PublishTeamHandler(_teamRepository, _itemVersionRepository, _currentUserService, Options.Create(PublishingOptionsFactory.Default(maxVersionsPerItem: 2)));
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorized_WhenUserIsNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        await AssertThrowsAsync<UnauthorizedCoreException>(Guid.NewGuid(), ErrorCodes.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFound_WhenTeamDoesNotExist()
    {
        _teamRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Team>, IQueryable<Team>>>(), Arg.Any<bool>()).Returns((Team?)null);

        await AssertThrowsAsync<NotFoundCoreException>(Guid.NewGuid(), ErrorCodes.TeamNotFound);
    }

    [Fact]
    public async Task Handle_ShouldThrowForbidden_WhenCallerIsNotTheOwner()
    {
        var team = StubTeam(TeamFactory.WithMembers(Guid.NewGuid(), TeamFactory.Pin("alpha")));

        await AssertThrowsAsync<ForbiddenCoreException>(team.Id, ErrorCodes.TeamNotOwned);
    }

    [Fact]
    public async Task Handle_ShouldThrowBadRequest_WhenTeamHasNoMembers()
    {
        var team = StubTeam(TeamFactory.Draft(_ownerUserId));

        await AssertThrowsAsync<BadRequestCoreException>(team.Id, ErrorCodes.TeamEmpty);
    }

    [Fact]
    public async Task Handle_ShouldThrowConflict_WhenAPublishIsAlreadyInProgress()
    {
        var team = StubTeam(TeamFactory.WithMembers(_ownerUserId, TeamFactory.Pin("alpha")));
        _itemVersionRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<ItemVersion, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<ItemVersion>, IQueryable<ItemVersion>>>(), Arg.Any<Func<IQueryable<ItemVersion>, IOrderedQueryable<ItemVersion>>>(), Arg.Any<bool>()).Returns(ItemVersionFactory.QueuedTeam(team.Id));

        await AssertThrowsAsync<ConflictCoreException>(team.Id, ErrorCodes.PublishAlreadyInProgress);
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRuleViolation_WhenVersionLimitIsReached()
    {
        var team = StubTeam(TeamFactory.WithMembers(_ownerUserId, TeamFactory.Pin("alpha")));
        _itemVersionRepository.CountAsync(Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<ItemVersion, bool>>>()).Returns(2);

        await AssertThrowsAsync<BusinessRuleViolationCoreException>(team.Id, ErrorCodes.PublishVersionLimitReached);
    }

    private Team StubTeam(Team team)
    {
        _teamRepository.GetByIdAsync(team.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Team>, IQueryable<Team>>>(), Arg.Any<bool>()).Returns(team);

        return team;
    }

    private async Task AssertThrowsAsync<TException>(Guid teamId, string errorCode) where TException : BaseException
    {
        var act = async () => await _sut.Handle(new PublishTeamCommand(teamId, VersionIncrement.Patch), CancellationToken.None);

        await act.Should().ThrowAsync<TException>().Where(x => x.ErrorCode == errorCode);
        await _itemVersionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
