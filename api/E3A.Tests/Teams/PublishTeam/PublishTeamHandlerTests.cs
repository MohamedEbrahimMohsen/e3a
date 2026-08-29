using System.Linq.Expressions;
using System.Text.Json;
using Core.Identity.Tokens.CurrentUser;
using E3A.Application.Teams.PublishTeam;
using E3A.Application.Teams.Shared;
using E3A.Domain.Publishing;
using E3A.Domain.Teams;
using E3A.Tests.Publishing.Shared;
using E3A.Tests.Teams.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace E3A.Tests.Teams.PublishTeam;

public sealed class PublishTeamHandlerTests
{
    private readonly ITeamRepository _teamRepository = Substitute.For<ITeamRepository>();
    private readonly IItemVersionRepository _itemVersionRepository = Substitute.For<IItemVersionRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _ownerUserId = Guid.NewGuid();
    private readonly PublishTeamHandler _sut;

    public PublishTeamHandlerTests()
    {
        _currentUserService.UserId.Returns(_ownerUserId);
        _sut = new PublishTeamHandler(_teamRepository, _itemVersionRepository, _currentUserService, Options.Create(PublishingOptionsFactory.Default()));
    }

    [Fact]
    public async Task Handle_ShouldCreateQueuedTeamVersion_WhenTeamHasMembers()
    {
        var team = StubTeam();

        var result = await _sut.Handle(new PublishTeamCommand(team.Id, VersionIncrement.Patch), CancellationToken.None);

        result.ItemId.Should().Be(team.Id);
        result.ItemType.Should().Be(nameof(ItemType.Team));
        result.VersionNumber.Should().Be(1);
        result.SemanticVersion.Should().Be("1.0.0");
        result.Status.Should().Be(nameof(ItemVersionStatus.Queued));
        await _itemVersionRepository.Received(1).AddAsync(Arg.Is<ItemVersion>(x => x.ItemType == ItemType.Team), Arg.Any<CancellationToken>());
        await _itemVersionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldFreezeTheRosterIntoTheVersion_WhenPublishing()
    {
        var team = StubTeam();
        ItemVersion? created = null;
        await _itemVersionRepository.AddAsync(Arg.Do<ItemVersion>(x => created = x), Arg.Any<CancellationToken>());

        await _sut.Handle(new PublishTeamCommand(team.Id, VersionIncrement.Patch), CancellationToken.None);

        var roster = JsonSerializer.Deserialize<TeamRosterResult>(created!.FrozenManifestJson)!;
        roster.Members.Should().HaveCount(team.Members.Count);
        roster.Members.Select(x => x.SortOrder).Should().Equal(0, 1);
        roster.Members.Select(x => x.EngineerId).Should().Equal(team.Members.Select(x => x.EngineerId));
        roster.Members.Select(x => x.EngineerSlug).Should().Equal("alpha", "beta");
        roster.Members.Select(x => x.PinnedVersionId).Should().Equal(team.Members.Select(x => x.PinnedVersionId));
        roster.Members.Select(x => x.PinnedSemanticVersion).Should().Equal(team.Members.Select(x => x.PinnedSemanticVersion));
    }

    [Fact]
    public async Task Handle_ShouldIncrementFromTheLatestVersion_WhenTeamHasPublishedBefore()
    {
        var team = StubTeam();
        var latest = ItemVersionFactory.QueuedTeam(team.Id, versionNumber: 1, semanticVersion: "1.0.0");
        _itemVersionRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<ItemVersion, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<ItemVersion>, IQueryable<ItemVersion>>>(), Arg.Is<Func<IQueryable<ItemVersion>, IOrderedQueryable<ItemVersion>>>(x => x != null), Arg.Any<bool>()).Returns(latest);

        var result = await _sut.Handle(new PublishTeamCommand(team.Id, VersionIncrement.Minor), CancellationToken.None);

        result.SemanticVersion.Should().Be("1.1.0");
        result.VersionNumber.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldRaisePublishRequestedEvent_WhenVersionIsCreated()
    {
        var team = StubTeam();
        ItemVersion? created = null;
        await _itemVersionRepository.AddAsync(Arg.Do<ItemVersion>(x => created = x), Arg.Any<CancellationToken>());

        await _sut.Handle(new PublishTeamCommand(team.Id, VersionIncrement.Patch), CancellationToken.None);

        created!.GetDomainEvents().Should().ContainSingle(x => x is PublishRequestedDomainEvent && ((PublishRequestedDomainEvent)x).VersionId == created.Id);
    }

    private Team StubTeam()
    {
        var team = TeamFactory.WithMembers(_ownerUserId, TeamFactory.Pin("alpha"), TeamFactory.Pin("beta"));
        _teamRepository.GetByIdAsync(team.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Team>, IQueryable<Team>>>(), Arg.Any<bool>()).Returns(team);

        return team;
    }
}
