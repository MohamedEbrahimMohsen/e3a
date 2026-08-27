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
    public async Task Handle_ShouldCreateEngineerWithBaseSlug_WhenSlugIsFreeAndUnderLimit()
    {
        _engineerRepository.CountAsync(Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Engineer, bool>>>()).Returns(0);
        _engineerRepository.IsSlugExistsAsync(EngineerFactory.DefaultSlug, Arg.Any<CancellationToken>()).Returns(false);

        var result = await _sut.Handle(new CreateEngineerCommand(EngineerFactory.DefaultDisplayName, "A backend engineer.", ["dotnet"]), CancellationToken.None);

        result.Slug.Should().Be(EngineerFactory.DefaultSlug);
        result.Status.Should().Be(nameof(EngineerStatus.Draft));
        result.InstallCount.Should().Be(0);
        _generator.DidNotReceive().Generate(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        await _engineerRepository.Received(1).AddAsync(Arg.Any<Engineer>(), Arg.Any<CancellationToken>());
        await _engineerRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldCreateEngineerWithSuffixedSlug_WhenBaseSlugIsAlreadyTaken()
    {
        var suffixedSlug = $"{EngineerFactory.DefaultSlug}-ab12";
        _engineerRepository.CountAsync(Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Engineer, bool>>>()).Returns(0);
        _engineerRepository.IsSlugExistsAsync(EngineerFactory.DefaultSlug, Arg.Any<CancellationToken>()).Returns(true);
        _engineerRepository.IsSlugExistsAsync(suffixedSlug, Arg.Any<CancellationToken>()).Returns(false);
        _generator.Generate(EngineerFactory.DefaultSlug, Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(suffixedSlug);

        var result = await _sut.Handle(new CreateEngineerCommand(EngineerFactory.DefaultDisplayName, null, []), CancellationToken.None);

        result.Slug.Should().Be(suffixedSlug);
        _generator.Received(1).Generate(EngineerFactory.DefaultSlug, Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        await _engineerRepository.Received(1).AddAsync(Arg.Is<Engineer>(x => x.Slug == suffixedSlug), Arg.Any<CancellationToken>());
        await _engineerRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldRetrySuffixedSlug_WhenFirstCandidateIsAlsoTaken()
    {
        var takenCandidate = $"{EngineerFactory.DefaultSlug}-ab12";
        var freeCandidate = $"{EngineerFactory.DefaultSlug}-cd34";
        _engineerRepository.CountAsync(Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Engineer, bool>>>()).Returns(0);
        _engineerRepository.IsSlugExistsAsync(EngineerFactory.DefaultSlug, Arg.Any<CancellationToken>()).Returns(true);
        _engineerRepository.IsSlugExistsAsync(takenCandidate, Arg.Any<CancellationToken>()).Returns(true);
        _engineerRepository.IsSlugExistsAsync(freeCandidate, Arg.Any<CancellationToken>()).Returns(false);
        _generator.Generate(EngineerFactory.DefaultSlug, Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(takenCandidate, freeCandidate);

        var result = await _sut.Handle(new CreateEngineerCommand(EngineerFactory.DefaultDisplayName, null, []), CancellationToken.None);

        result.Slug.Should().Be(freeCandidate);
        _generator.Received(2).Generate(EngineerFactory.DefaultSlug, Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorized_WhenUserIsNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = async () => await _sut.Handle(new CreateEngineerCommand(EngineerFactory.DefaultDisplayName, null, []), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedCoreException>().Where(x => x.ErrorCode == ErrorCodes.UserNotAuthenticated);
        await _engineerRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRuleViolation_WhenCreatorReachedTheLimit()
    {
        _engineerRepository.CountAsync(Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Engineer, bool>>>()).Returns(2);

        var act = async () => await _sut.Handle(new CreateEngineerCommand(EngineerFactory.DefaultDisplayName, null, []), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleViolationCoreException>().Where(x => x.ErrorCode == ErrorCodes.EngineerLimitReached);
        await _engineerRepository.DidNotReceive().AddAsync(Arg.Any<Engineer>(), Arg.Any<CancellationToken>());
        await _engineerRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
