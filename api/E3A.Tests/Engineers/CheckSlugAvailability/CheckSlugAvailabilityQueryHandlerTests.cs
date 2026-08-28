using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Utilities.Generator;
using E3A.Application.Engineers.CheckSlugAvailability;
using E3A.Application.Exceptions;
using E3A.Domain.Engineers;
using E3A.Tests.Engineers.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace E3A.Tests.Engineers.CheckSlugAvailability;

public sealed class CheckSlugAvailabilityQueryHandlerTests
{
    private const string TypedSlug = "mmohsen";
    private const string SuffixedSlug = "mmohsen-ab12";

    private readonly IEngineerRepository _engineerRepository = Substitute.For<IEngineerRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IGenerator _generator = Substitute.For<IGenerator>();
    private readonly CheckSlugAvailabilityQueryHandler _sut;

    public CheckSlugAvailabilityQueryHandlerTests()
    {
        _currentUserService.UserId.Returns(Guid.NewGuid());
        _sut = new CheckSlugAvailabilityQueryHandler(_engineerRepository, _currentUserService, _generator, Options.Create(EngineerFactory.CreateEngineersOptions()));
    }

    [Fact]
    public async Task Handle_ShouldReturnAvailable_WhenSlugIsFree()
    {
        _engineerRepository.IsSlugExistsAsync(TypedSlug, Arg.Any<CancellationToken>()).Returns(false);

        var result = await _sut.Handle(new CheckSlugAvailabilityQuery(TypedSlug), CancellationToken.None);

        result.Slug.Should().Be(TypedSlug);
        result.IsAvailable.Should().BeTrue();
        result.SuggestedSlug.Should().BeNull();
        _generator.DidNotReceive().Generate(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_ShouldReturnUnavailableWithSuggestion_WhenSlugIsTaken()
    {
        _engineerRepository.IsSlugExistsAsync(TypedSlug, Arg.Any<CancellationToken>()).Returns(true);
        _engineerRepository.IsSlugExistsAsync(SuffixedSlug, Arg.Any<CancellationToken>()).Returns(false);
        _generator.Generate(TypedSlug, Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns($"{SuffixedSlug}-");

        var result = await _sut.Handle(new CheckSlugAvailabilityQuery(TypedSlug), CancellationToken.None);

        result.IsAvailable.Should().BeFalse();
        result.SuggestedSlug.Should().Be(SuffixedSlug);
    }

    [Fact]
    public async Task Handle_ShouldReturnNormalizedSlug_WhenSlugHasUppercaseAndWhitespace()
    {
        _engineerRepository.IsSlugExistsAsync(TypedSlug, Arg.Any<CancellationToken>()).Returns(false);

        var result = await _sut.Handle(new CheckSlugAvailabilityQuery("  MMohsen  "), CancellationToken.None);

        result.Slug.Should().Be(TypedSlug);
        await _engineerRepository.Received(1).IsSlugExistsAsync(TypedSlug, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorized_WhenUserIsNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = async () => await _sut.Handle(new CheckSlugAvailabilityQuery(TypedSlug), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedCoreException>().Where(x => x.ErrorCode == ErrorCodes.UserNotAuthenticated);
        await _engineerRepository.DidNotReceive().IsSlugExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
