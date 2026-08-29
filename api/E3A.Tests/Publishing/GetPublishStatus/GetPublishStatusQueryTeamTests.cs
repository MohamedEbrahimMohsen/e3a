using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using E3A.Application.Exceptions;
using E3A.Application.Publishing.GetPublishStatus;
using E3A.Domain.Engineers;
using E3A.Domain.Publishing;
using E3A.Domain.Teams;
using E3A.Tests.Publishing.Shared;
using E3A.Tests.Teams.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace E3A.Tests.Publishing.GetPublishStatus;

public sealed class GetPublishStatusQueryTeamTests
{
    private readonly IItemVersionRepository _itemVersionRepository = Substitute.For<IItemVersionRepository>();
    private readonly IEngineerRepository _engineerRepository = Substitute.For<IEngineerRepository>();
    private readonly ITeamRepository _teamRepository = Substitute.For<ITeamRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _ownerUserId = Guid.NewGuid();
    private readonly GetPublishStatusQueryHandler _sut;

    public GetPublishStatusQueryTeamTests()
    {
        _currentUserService.UserId.Returns(_ownerUserId);
        _sut = new GetPublishStatusQueryHandler(_itemVersionRepository, _engineerRepository, _teamRepository, _currentUserService, Options.Create(PublishingOptionsFactory.Default()));
    }

    [Fact]
    public async Task Handle_ShouldReturnStatus_WhenVersionIsATeamVersionOwnedByCaller()
    {
        var team = TeamFactory.Draft(_ownerUserId);
        var version = GivenTeamVersion(team.Id);
        StubTeam(team.Id, team);

        var result = await _sut.Handle(new GetPublishStatusQuery(version.Id), CancellationToken.None);

        result.ItemId.Should().Be(team.Id);
        result.ItemType.Should().Be(nameof(ItemType.Team));
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFound_WhenTeamVersionHasNoTeam()
    {
        var version = GivenTeamVersion(Guid.NewGuid());
        StubTeam(version.ItemId, null);

        var act = async () => await _sut.Handle(new GetPublishStatusQuery(version.Id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundCoreException>().Where(x => x.ErrorCode == ErrorCodes.TeamNotFound);
    }

    [Fact]
    public async Task Handle_ShouldThrowForbidden_WhenTeamVersionBelongsToAnotherCreator()
    {
        var team = TeamFactory.Draft(Guid.NewGuid());
        var version = GivenTeamVersion(team.Id);
        StubTeam(team.Id, team);

        var act = async () => await _sut.Handle(new GetPublishStatusQuery(version.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenCoreException>().Where(x => x.ErrorCode == ErrorCodes.TeamNotOwned);
    }

    private ItemVersion GivenTeamVersion(Guid teamId)
    {
        var version = ItemVersionFactory.QueuedTeam(teamId);
        _itemVersionRepository.GetByIdAsync(version.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<ItemVersion>, IQueryable<ItemVersion>>>(), Arg.Any<bool>()).Returns(version);

        return version;
    }

    private void StubTeam(Guid teamId, Team? team)
        => _teamRepository.GetByIdAsync(teamId, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Team>, IQueryable<Team>>>(), Arg.Any<bool>()).Returns(team);
}
