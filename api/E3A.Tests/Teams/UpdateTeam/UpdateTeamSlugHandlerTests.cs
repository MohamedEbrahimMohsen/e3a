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

public sealed class UpdateTeamSlugHandlerTests
{
    private const string NewSlug = "platform-squad";

    private readonly ITeamRepository _teamRepository = Substitute.For<ITeamRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IGenerator _generator = Substitute.For<IGenerator>();
    private readonly Guid _ownerUserId = Guid.NewGuid();
    private readonly UpdateTeamHandler _sut;

    public UpdateTeamSlugHandlerTests()
    {
        _currentUserService.UserId.Returns(_ownerUserId);
        _sut = new UpdateTeamHandler(_teamRepository, _currentUserService, _generator, Options.Create(TeamFactory.CreateTeamsOptions()));
    }

    [Fact]
    public async Task Handle_ShouldChangeSlug_WhenTeamHasNeverPublished()
    {
        var team = TeamFactory.Draft(_ownerUserId);
        _teamRepository.GetByIdAsync(team.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Team>, IQueryable<Team>>>(), Arg.Any<bool>()).Returns(team);
        _teamRepository.IsSlugExistsAsync(NewSlug, Arg.Any<CancellationToken>()).Returns(false);

        var result = await _sut.Handle(new UpdateTeamCommand(team.Id, NewSlug, TeamFactory.DefaultDisplayName, null, []), CancellationToken.None);

        result.Slug.Should().Be(NewSlug);
        await _teamRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldNotChangeSlug_WhenSlugIsUnchanged()
    {
        var team = TeamFactory.Draft(_ownerUserId);
        _teamRepository.GetByIdAsync(team.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Team>, IQueryable<Team>>>(), Arg.Any<bool>()).Returns(team);

        var result = await _sut.Handle(new UpdateTeamCommand(team.Id, TeamFactory.DefaultSlug, TeamFactory.DefaultDisplayName, null, []), CancellationToken.None);

        result.Slug.Should().Be(TeamFactory.DefaultSlug);
        await _teamRepository.DidNotReceive().IsSlugExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRuleViolation_WhenSlugIsFrozen()
    {
        var team = TeamFactory.Published(_ownerUserId);
        _teamRepository.GetByIdAsync(team.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Team>, IQueryable<Team>>>(), Arg.Any<bool>()).Returns(team);

        var act = async () => await _sut.Handle(new UpdateTeamCommand(team.Id, NewSlug, TeamFactory.DefaultDisplayName, null, []), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleViolationCoreException>().Where(x => x.ErrorCode == ErrorCodes.TeamSlugFrozen);
        await _teamRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
