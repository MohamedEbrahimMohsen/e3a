using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Utilities.Generator;
using E3A.Application.Exceptions;
using E3A.Application.Teams.CheckTeamSlugAvailability;
using E3A.Domain.Teams;
using E3A.Tests.Teams.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace E3A.Tests.Teams.CheckTeamSlugAvailability;

public sealed class CheckTeamSlugAvailabilityQueryHandlerTests
{
    private const string TypedSlug = "dotnet-product-squad";
    private const string SuffixedSlug = "dotnet-product-squad-ab12";

    private readonly ITeamRepository _teamRepository = Substitute.For<ITeamRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IGenerator _generator = Substitute.For<IGenerator>();
    private readonly CheckTeamSlugAvailabilityQueryHandler _sut;

    public CheckTeamSlugAvailabilityQueryHandlerTests()
    {
        _currentUserService.UserId.Returns(Guid.NewGuid());
        _sut = new CheckTeamSlugAvailabilityQueryHandler(_teamRepository, _currentUserService, _generator, Options.Create(TeamFactory.CreateTeamsOptions()));
    }

    [Fact]
    public async Task Handle_ShouldReturnAvailable_WhenSlugIsFree()
    {
        _teamRepository.IsSlugExistsAsync(TypedSlug, Arg.Any<CancellationToken>()).Returns(false);

        var result = await _sut.Handle(new CheckTeamSlugAvailabilityQuery(TypedSlug), CancellationToken.None);

        result.Slug.Should().Be(TypedSlug);
        result.IsAvailable.Should().BeTrue();
        result.SuggestedSlug.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldReturnSuggestion_WhenSlugIsTaken()
    {
        _teamRepository.IsSlugExistsAsync(TypedSlug, Arg.Any<CancellationToken>()).Returns(true);
        _teamRepository.IsSlugExistsAsync(SuffixedSlug, Arg.Any<CancellationToken>()).Returns(false);
        _generator.Generate(TypedSlug, Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns($"{SuffixedSlug}-");

        var result = await _sut.Handle(new CheckTeamSlugAvailabilityQuery(TypedSlug), CancellationToken.None);

        result.IsAvailable.Should().BeFalse();
        result.SuggestedSlug.Should().NotBeNullOrEmpty();
        result.SuggestedSlug.Should().NotBe(TypedSlug);
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorized_WhenUserIsNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = async () => await _sut.Handle(new CheckTeamSlugAvailabilityQuery(TypedSlug), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedCoreException>().Where(x => x.ErrorCode == ErrorCodes.UserNotAuthenticated);
    }
}
