using System.Linq.Expressions;
using Core.Identity.Tokens.CurrentUser;
using E3A.Application.Teams.SetTeamMembers;
using E3A.Domain.Engineers;
using E3A.Domain.Publishing;
using E3A.Domain.Teams;
using E3A.Tests.Engineers.Shared;
using E3A.Tests.Publishing.Shared;
using E3A.Tests.Teams.Shared;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace E3A.Tests.Teams.SetTeamMembers;

public sealed class SetTeamMembersHandlerTests
{
    private readonly ITeamRepository _teamRepository = Substitute.For<ITeamRepository>();
    private readonly IEngineerRepository _engineerRepository = Substitute.For<IEngineerRepository>();
    private readonly IItemVersionRepository _itemVersionRepository = Substitute.For<IItemVersionRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _ownerUserId = Guid.NewGuid();
    private readonly SetTeamMembersHandler _sut;

    public SetTeamMembersHandlerTests()
    {
        _currentUserService.UserId.Returns(_ownerUserId);
        _sut = new SetTeamMembersHandler(_teamRepository, _engineerRepository, _itemVersionRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_ShouldReplaceMembersInSubmittedOrder_WhenRequestIsValid()
    {
        var team = TeamFactory.Draft(_ownerUserId);
        var first = TeamFactory.PublishedMember("alpha");
        var second = TeamFactory.PublishedMember("beta");
        var third = TeamFactory.PublishedMember("gamma");
        Stub(team, [first, second, third]);

        var result = await _sut.Handle(new SetTeamMembersCommand(team.Id, [Select(third), Select(first), Select(second)]), CancellationToken.None);

        result.Members.Select(x => x.EngineerSlug).Should().Equal("gamma", "alpha", "beta");
        result.Members.Select(x => x.SortOrder).Should().Equal(0, 1, 2);
        await _teamRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldPinToLatestVersion_WhenPinnedVersionIsNull()
    {
        var team = TeamFactory.Draft(_ownerUserId);
        var member = TeamFactory.PublishedMember("alpha", semanticVersion: "2.3.4");
        Stub(team, [member]);

        var result = await _sut.Handle(new SetTeamMembersCommand(team.Id, [new TeamMemberSelection(member.Engineer.Id, null)]), CancellationToken.None);

        result.Members[0].PinnedVersionId.Should().Be(member.Engineer.LatestVersionId!.Value);
        result.Members[0].PinnedSemanticVersion.Should().Be("2.3.4");
    }

    [Fact]
    public async Task Handle_ShouldPinToExplicitVersion_WhenPinnedVersionIsGiven()
    {
        var team = TeamFactory.Draft(_ownerUserId);
        var member = TeamFactory.PublishedMember("alpha");
        var older = ItemVersionFactory.Published(member.Engineer.Id, versionNumber: 1, semanticVersion: "0.9.0");
        Stub(team, [member], extraVersions: [older]);

        var result = await _sut.Handle(new SetTeamMembersCommand(team.Id, [new TeamMemberSelection(member.Engineer.Id, older.Id)]), CancellationToken.None);

        result.Members[0].PinnedVersionId.Should().Be(older.Id);
        result.Members[0].PinnedVersionId.Should().NotBe(member.Engineer.LatestVersionId!.Value);
    }

    [Fact]
    public async Task Handle_ShouldKeepExistingPin_WhenMemberIsAlreadyInTheTeamAndPinIsNull()
    {
        var member = TeamFactory.PublishedMember("alpha");
        var older = ItemVersionFactory.Published(member.Engineer.Id, versionNumber: 1, semanticVersion: "0.9.0");
        var team = TeamFactory.WithMembers(_ownerUserId, new TeamMemberPin(member.Engineer.Id, "alpha", older.Id, "0.9.0"));
        Stub(team, [member], extraVersions: [older]);

        var result = await _sut.Handle(new SetTeamMembersCommand(team.Id, [new TeamMemberSelection(member.Engineer.Id, null)]), CancellationToken.None);

        result.Members[0].PinnedVersionId.Should().Be(older.Id);
        result.Members[0].PinnedSemanticVersion.Should().Be("0.9.0");
    }

    [Fact]
    public async Task Handle_ShouldRemoveMembers_WhenSubmittedListIsEmpty()
    {
        var team = TeamFactory.WithMembers(_ownerUserId, TeamFactory.Pin("alpha"));
        Stub(team, []);

        var result = await _sut.Handle(new SetTeamMembersCommand(team.Id, []), CancellationToken.None);

        result.Members.Should().BeEmpty();
        await _teamRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _engineerRepository.DidNotReceive().FindAsync(Arg.Any<Expression<Func<Engineer, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Engineer>, IQueryable<Engineer>>>(), Arg.Any<Func<IQueryable<Engineer>, IOrderedQueryable<Engineer>>>(), Arg.Any<bool>());
        await _itemVersionRepository.DidNotReceive().FindAsync(Arg.Any<Expression<Func<ItemVersion, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<ItemVersion>, IQueryable<ItemVersion>>>(), Arg.Any<Func<IQueryable<ItemVersion>, IOrderedQueryable<ItemVersion>>>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task Handle_ShouldDenormaliseSlugAndSemanticVersion_WhenReplacing()
    {
        var team = TeamFactory.Draft(_ownerUserId);
        var member = TeamFactory.PublishedMember("alpha", semanticVersion: "4.5.6");
        Stub(team, [member]);

        var result = await _sut.Handle(new SetTeamMembersCommand(team.Id, [Select(member)]), CancellationToken.None);

        result.Members[0].EngineerSlug.Should().Be("alpha");
        result.Members[0].PinnedSemanticVersion.Should().Be("4.5.6");
    }

    private static TeamMemberSelection Select(TeamMemberFixture member) => new(member.Engineer.Id, null);

    private void Stub(Team team, List<TeamMemberFixture> members, List<ItemVersion>? extraVersions = null)
    {
        _teamRepository.GetByIdAsync(team.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Team>, IQueryable<Team>>>(), Arg.Any<bool>()).Returns(team);
        _engineerRepository.FindAsync(Arg.Any<Expression<Func<Engineer, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Engineer>, IQueryable<Engineer>>>(), Arg.Any<Func<IQueryable<Engineer>, IOrderedQueryable<Engineer>>>(), Arg.Any<bool>()).Returns([.. members.Select(x => x.Engineer)]);
        _itemVersionRepository.FindAsync(Arg.Any<Expression<Func<ItemVersion, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<ItemVersion>, IQueryable<ItemVersion>>>(), Arg.Any<Func<IQueryable<ItemVersion>, IOrderedQueryable<ItemVersion>>>(), Arg.Any<bool>()).Returns([.. members.Select(x => x.Version), .. extraVersions ?? []]);
    }
}
