using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Utilities.Generator;
using E3A.Application.Engineers.CreateEngineer;
using E3A.Application.Exceptions;
using E3A.Domain.Engineers;
using E3A.Tests.Engineers.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;
using Xunit;

namespace E3A.Tests.Engineers.CreateEngineer;

public sealed class CreateEngineerHandlerTests
{
    private const string TypedSlug = "mmohsen";
    private const string SuffixedSlug = "mmohsen-ab12";

    private readonly IEngineerRepository _engineerRepository = Substitute.For<IEngineerRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IGenerator _generator = Substitute.For<IGenerator>();
    private readonly Guid _ownerUserId = Guid.NewGuid();
    private readonly CreateEngineerHandler _sut;

    public CreateEngineerHandlerTests()
    {
        _currentUserService.UserId.Returns(_ownerUserId);
        _sut = new CreateEngineerHandler(_engineerRepository, _currentUserService, _generator, Options.Create(EngineerFactory.CreateEngineersOptions(maxEngineersPerCreator: 2)));
    }

    [Fact]
    public async Task Handle_ShouldCreateEngineerWithTypedSlug_WhenSlugIsFree()
    {
        _engineerRepository.CountAsync(Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Engineer, bool>>>()).Returns(0);
        _engineerRepository.IsSlugExistsAsync(TypedSlug, Arg.Any<CancellationToken>()).Returns(false);

        var result = await _sut.Handle(new CreateEngineerCommand(TypedSlug, EngineerFactory.DefaultDisplayName, "A backend engineer.", ["dotnet"]), CancellationToken.None);

        result.Slug.Should().Be(TypedSlug);
        result.Status.Should().Be(nameof(EngineerStatus.Draft));
        result.InstallCount.Should().Be(0);
        await _engineerRepository.Received(1).AddAsync(Arg.Any<Engineer>(), Arg.Any<CancellationToken>());
        await _engineerRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldNormalizeTypedSlug_WhenSlugHasUppercaseAndWhitespace()
    {
        _engineerRepository.CountAsync(Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Engineer, bool>>>()).Returns(0);
        _engineerRepository.IsSlugExistsAsync(TypedSlug, Arg.Any<CancellationToken>()).Returns(false);

        var result = await _sut.Handle(new CreateEngineerCommand("  MMohsen  ", EngineerFactory.DefaultDisplayName, null, []), CancellationToken.None);

        result.Slug.Should().Be(TypedSlug);
        await _engineerRepository.Received(1).AddAsync(Arg.Is<Engineer>(x => x.Slug == TypedSlug), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldCreateEngineerWithSuffixedSlug_WhenTypedSlugIsTaken()
    {
        _engineerRepository.CountAsync(Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Engineer, bool>>>()).Returns(0);
        _engineerRepository.IsSlugExistsAsync(TypedSlug, Arg.Any<CancellationToken>()).Returns(true);
        _engineerRepository.IsSlugExistsAsync(SuffixedSlug, Arg.Any<CancellationToken>()).Returns(false);
        _generator.Generate(TypedSlug, Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns($"{SuffixedSlug}-");

        var result = await _sut.Handle(new CreateEngineerCommand(TypedSlug, EngineerFactory.DefaultDisplayName, null, []), CancellationToken.None);

        result.Slug.Should().Be(SuffixedSlug);
        await _engineerRepository.Received(1).AddAsync(Arg.Is<Engineer>(x => x.Slug == SuffixedSlug), Arg.Any<CancellationToken>());
        await _engineerRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorized_WhenUserIsNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = async () => await _sut.Handle(new CreateEngineerCommand(TypedSlug, EngineerFactory.DefaultDisplayName, null, []), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedCoreException>().Where(x => x.ErrorCode == ErrorCodes.UserNotAuthenticated);
        await _engineerRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRuleViolation_WhenCreatorReachedTheLimit()
    {
        _engineerRepository.CountAsync(Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Engineer, bool>>>()).Returns(2);

        var act = async () => await _sut.Handle(new CreateEngineerCommand(TypedSlug, EngineerFactory.DefaultDisplayName, null, []), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleViolationCoreException>().Where(x => x.ErrorCode == ErrorCodes.EngineerLimitReached);
        await _engineerRepository.DidNotReceive().AddAsync(Arg.Any<Engineer>(), Arg.Any<CancellationToken>());
        await _engineerRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
