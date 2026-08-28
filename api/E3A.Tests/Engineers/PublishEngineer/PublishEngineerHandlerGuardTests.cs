using System.Linq.Expressions;
using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using E3A.Application.Engineers.PublishEngineer;
using E3A.Application.Exceptions;
using E3A.Domain.Engineers;
using E3A.Domain.Publishing;
using E3A.Tests.Engineers.Shared;
using E3A.Tests.Publishing.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace E3A.Tests.Engineers.PublishEngineer;

public sealed class PublishEngineerHandlerGuardTests
{
    private const int VersionLimit = 3;
    private readonly IEngineerRepository _engineerRepository = Substitute.For<IEngineerRepository>();
    private readonly IItemVersionRepository _itemVersionRepository = Substitute.For<IItemVersionRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _ownerUserId = Guid.NewGuid();
    private readonly Engineer _engineer;
    private readonly PublishEngineerHandler _sut;

    public PublishEngineerHandlerGuardTests()
    {
        _engineer = EngineerFactory.Draft(_ownerUserId);
        _engineer.ReplaceDraftManifest("{\"imported\":[]}");
        _currentUserService.UserId.Returns(_ownerUserId);
        _engineerRepository.GetByIdAsync(_engineer.Id, Arg.Any<CancellationToken>()).Returns(_engineer);
        _sut = new PublishEngineerHandler(_engineerRepository, _itemVersionRepository, _currentUserService, Options.Create(PublishingOptionsFactory.Default(maxVersionsPerItem: VersionLimit)));
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorized_WhenUserIsNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        await Act().Should().ThrowAsync<UnauthorizedCoreException>().Where(x => x.ErrorCode == ErrorCodes.UserNotAuthenticated);
        await _itemVersionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFound_WhenEngineerDoesNotExist()
    {
        _engineerRepository.GetByIdAsync(_engineer.Id, Arg.Any<CancellationToken>()).Returns((Engineer?)null);

        await Act().Should().ThrowAsync<NotFoundCoreException>().Where(x => x.ErrorCode == ErrorCodes.EngineerNotFound);
        await _itemVersionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowForbidden_WhenCallerIsNotOwner()
    {
        _currentUserService.UserId.Returns(Guid.NewGuid());

        await Act().Should().ThrowAsync<ForbiddenCoreException>().Where(x => x.ErrorCode == ErrorCodes.EngineerNotOwned);
        await _itemVersionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowBadRequest_WhenDraftWasNeverUploaded()
    {
        var withoutDraft = EngineerFactory.Draft(_ownerUserId);
        _engineerRepository.GetByIdAsync(withoutDraft.Id, Arg.Any<CancellationToken>()).Returns(withoutDraft);

        var act = async () => await _sut.Handle(new PublishEngineerCommand(withoutDraft.Id, VersionIncrement.Patch), CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestCoreException>().Where(x => x.ErrorCode == ErrorCodes.EngineerDraftNotUploaded);
        await _itemVersionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowConflict_WhenAVersionIsAlreadyQueuedOrBuilding()
    {
        _itemVersionRepository
            .FirstOrDefaultAsync(Arg.Any<Expression<Func<ItemVersion, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<ItemVersion>, IQueryable<ItemVersion>>?>(), Arg.Any<Func<IQueryable<ItemVersion>, IOrderedQueryable<ItemVersion>>?>(), Arg.Any<bool>())
            .Returns(ItemVersionFactory.Building(_engineer.Id));

        await Act().Should().ThrowAsync<ConflictCoreException>().Where(x => x.ErrorCode == ErrorCodes.PublishAlreadyInProgress);
        await _itemVersionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRuleViolation_WhenVersionCapIsReached()
    {
        _itemVersionRepository.CountAsync(Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<ItemVersion, bool>>?>()).Returns(VersionLimit);

        await Act().Should().ThrowAsync<BusinessRuleViolationCoreException>()
            .Where(x => x.ErrorCode == ErrorCodes.PublishVersionLimitReached && x.Context != null && (int)x.Context["limit"] == VersionLimit);
        await _itemVersionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private Func<Task> Act()
    {
        return async () => await _sut.Handle(new PublishEngineerCommand(_engineer.Id, VersionIncrement.Patch), CancellationToken.None);
    }
}
