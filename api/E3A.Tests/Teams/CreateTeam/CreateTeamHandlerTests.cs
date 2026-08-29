using System.Linq.Expressions;
using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Utilities.Generator;
using E3A.Application.Exceptions;
using E3A.Application.Teams.CreateTeam;
using E3A.Domain.Teams;
using E3A.Tests.Teams.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace E3A.Tests.Teams.CreateTeam;

public sealed class CreateTeamHandlerTests
{
    private const string TypedSlug = "dotnet-product-squad";
    private const string SuffixedSlug = "dotnet-product-squad-ab12";

    private readonly ITeamRepository _teamRepository = Substitute.For<ITeamRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IGenerator _generator = Substitute.For<IGenerator>();
    private readonly CreateTeamHandler _sut;

    public CreateTeamHandlerTests()
    {
        _currentUserService.UserId.Returns(Guid.NewGuid());
        _sut = new CreateTeamHandler(_teamRepository, _currentUserService, _generator, Options.Create(TeamFactory.CreateTeamsOptions(maxTeamsPerCreator: 2)));
    }

    [Fact]
    public async Task Handle_ShouldCreateTeam_WhenRequestIsValid()
    {
        _teamRepository.CountAsync(Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Team, bool>>>()).Returns(0);
        _teamRepository.IsSlugExistsAsync(TypedSlug, Arg.Any<CancellationToken>()).Returns(false);

        var result = await _sut.Handle(new CreateTeamCommand("  DotNet-Product-Squad  ", TeamFactory.DefaultDisplayName, "A product squad.", ["dotnet"]), CancellationToken.None);

        result.Slug.Should().Be(TypedSlug);
        result.Status.Should().Be(nameof(TeamStatus.Draft));
        result.MemberCount.Should().Be(0);
        await _teamRepository.Received(1).AddAsync(Arg.Any<Team>(), Arg.Any<CancellationToken>());
        await _teamRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorized_WhenUserIsNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = async () => await _sut.Handle(new CreateTeamCommand(TypedSlug, TeamFactory.DefaultDisplayName, null, []), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedCoreException>().Where(x => x.ErrorCode == ErrorCodes.UserNotAuthenticated);
        await _teamRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRuleViolation_WhenTeamLimitIsReached()
    {
        _teamRepository.CountAsync(Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Team, bool>>>()).Returns(2);

        var act = async () => await _sut.Handle(new CreateTeamCommand(TypedSlug, TeamFactory.DefaultDisplayName, null, []), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleViolationCoreException>().Where(x => x.ErrorCode == ErrorCodes.TeamLimitReached);
        await _teamRepository.DidNotReceive().AddAsync(Arg.Any<Team>(), Arg.Any<CancellationToken>());
        await _teamRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldUseSuffixedSlug_WhenSlugIsTaken()
    {
        _teamRepository.CountAsync(Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Team, bool>>>()).Returns(0);
        _teamRepository.IsSlugExistsAsync(TypedSlug, Arg.Any<CancellationToken>()).Returns(true);
        _teamRepository.IsSlugExistsAsync(SuffixedSlug, Arg.Any<CancellationToken>()).Returns(false);
        _generator.Generate(TypedSlug, Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns($"{SuffixedSlug}-");

        var result = await _sut.Handle(new CreateTeamCommand(TypedSlug, TeamFactory.DefaultDisplayName, null, []), CancellationToken.None);

        result.Slug.Should().NotBe(TypedSlug);
        result.Slug.Should().NotBeEmpty();
        await _teamRepository.Received(1).AddAsync(Arg.Is<Team>(x => x.Slug == SuffixedSlug), Arg.Any<CancellationToken>());
    }
}
