using System.Linq.Expressions;
using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using E3A.Application.Exceptions;
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

public sealed class SetTeamMembersHandlerGuardTests
{
    private readonly ITeamRepository _teamRepository = Substitute.For<ITeamRepository>();
    private readonly IEngineerRepository _engineerRepository = Substitute.For<IEngineerRepository>();
    private readonly IItemVersionRepository _itemVersionRepository = Substitute.For<IItemVersionRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _ownerUserId = Guid.NewGuid();
    private readonly SetTeamMembersHandler _sut;

    public SetTeamMembersHandlerGuardTests()
    {
        _currentUserService.UserId.Returns(_ownerUserId);
        _sut = new SetTeamMembersHandler(_teamRepository, _engineerRepository, _itemVersionRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorized_WhenUserIsNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        await AssertThrowsAsync<UnauthorizedCoreException>(new SetTeamMembersCommand(Guid.NewGuid(), []), ErrorCodes.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFound_WhenTeamDoesNotExist()
    {
        _teamRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Team>, IQueryable<Team>>>(), Arg.Any<bool>()).Returns((Team?)null);

        await AssertThrowsAsync<NotFoundCoreException>(new SetTeamMembersCommand(Guid.NewGuid(), []), ErrorCodes.TeamNotFound);
    }

    [Fact]
    public async Task Handle_ShouldThrowForbidden_WhenCallerIsNotTheOwner()
    {
        var team = TeamFactory.Draft(Guid.NewGuid());
        Stub(team, [], []);

        await AssertThrowsAsync<ForbiddenCoreException>(new SetTeamMembersCommand(team.Id, []), ErrorCodes.TeamNotOwned);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFound_WhenMemberEngineerDoesNotExist()
    {
        var team = TeamFactory.Draft(_ownerUserId);
        Stub(team, [], []);

        await AssertThrowsAsync<NotFoundCoreException>(new SetTeamMembersCommand(team.Id, [new TeamMemberSelection(Guid.NewGuid(), null)]), ErrorCodes.EngineerNotFound);
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRuleViolation_WhenMemberHasNeverPublished()
    {
        var team = TeamFactory.Draft(_ownerUserId);
        var engineer = EngineerFactory.Draft(Guid.NewGuid(), slug: "alpha");
        Stub(team, [engineer], []);

        await AssertThrowsAsync<BusinessRuleViolationCoreException>(new SetTeamMembersCommand(team.Id, [new TeamMemberSelection(engineer.Id, null)]), ErrorCodes.TeamMemberNotPublished);
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRuleViolation_WhenPinnedVersionIsNotPublished()
    {
        var team = TeamFactory.Draft(_ownerUserId);
        var engineer = EngineerFactory.Draft(Guid.NewGuid(), slug: "alpha");
        var queued = ItemVersionFactory.Queued(engineer.Id);
        Stub(team, [engineer], [queued]);

        await AssertThrowsAsync<BusinessRuleViolationCoreException>(new SetTeamMembersCommand(team.Id, [new TeamMemberSelection(engineer.Id, queued.Id)]), ErrorCodes.TeamMemberVersionNotPublished);
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRuleViolation_WhenPinnedVersionBelongsToAnotherEngineer()
    {
        var team = TeamFactory.Draft(_ownerUserId);
        var member = TeamFactory.PublishedMember("alpha");
        var otherVersion = ItemVersionFactory.Published(Guid.NewGuid(), semanticVersion: "5.0.0");
        Stub(team, [member.Engineer], [member.Version, otherVersion]);

        await AssertThrowsAsync<BusinessRuleViolationCoreException>(new SetTeamMembersCommand(team.Id, [new TeamMemberSelection(member.Engineer.Id, otherVersion.Id)]), ErrorCodes.TeamMemberVersionNotPublished);
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRuleViolation_WhenPinnedVersionIsATeamVersion()
    {
        var team = TeamFactory.Draft(_ownerUserId);
        var member = TeamFactory.PublishedMember("alpha");
        var teamVersion = ItemVersionFactory.QueuedTeam(member.Engineer.Id);
        Stub(team, [member.Engineer], [member.Version, teamVersion]);

        await AssertThrowsAsync<BusinessRuleViolationCoreException>(new SetTeamMembersCommand(team.Id, [new TeamMemberSelection(member.Engineer.Id, teamVersion.Id)]), ErrorCodes.TeamMemberVersionNotPublished);
    }

    [Fact]
    public async Task Handle_ShouldAddMember_WhenMemberEngineerIsUnlistedButHasAPublishedVersion()
    {
        var team = TeamFactory.Draft(_ownerUserId);
        var member = TeamFactory.PublishedMember("alpha");
        member.Engineer.Unlist();
        Stub(team, [member.Engineer], [member.Version]);

        var result = await _sut.Handle(new SetTeamMembersCommand(team.Id, [new TeamMemberSelection(member.Engineer.Id, null)]), CancellationToken.None);

        result.Members.Should().ContainSingle();
        await _teamRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldAddMember_WhenMemberEngineerBelongsToAnotherCreator()
    {
        var team = TeamFactory.Draft(_ownerUserId);
        var member = TeamFactory.PublishedMember("alpha", ownerUserId: Guid.NewGuid());
        Stub(team, [member.Engineer], [member.Version]);

        var result = await _sut.Handle(new SetTeamMembersCommand(team.Id, [new TeamMemberSelection(member.Engineer.Id, null)]), CancellationToken.None);

        result.Members.Should().ContainSingle();
        await _teamRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private async Task AssertThrowsAsync<TException>(SetTeamMembersCommand command, string errorCode) where TException : BaseException
    {
        var act = async () => await _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<TException>().Where(x => x.ErrorCode == errorCode);
        await _teamRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private void Stub(Team team, List<Engineer> engineers, List<ItemVersion> versions)
    {
        _teamRepository.GetByIdAsync(team.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Team>, IQueryable<Team>>>(), Arg.Any<bool>()).Returns(team);
        _engineerRepository.FindAsync(Arg.Any<Expression<Func<Engineer, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Engineer>, IQueryable<Engineer>>>(), Arg.Any<Func<IQueryable<Engineer>, IOrderedQueryable<Engineer>>>(), Arg.Any<bool>()).Returns(engineers);
        _itemVersionRepository.FindAsync(Arg.Any<Expression<Func<ItemVersion, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<ItemVersion>, IQueryable<ItemVersion>>>(), Arg.Any<Func<IQueryable<ItemVersion>, IOrderedQueryable<ItemVersion>>>(), Arg.Any<bool>()).Returns(versions);
    }
}
