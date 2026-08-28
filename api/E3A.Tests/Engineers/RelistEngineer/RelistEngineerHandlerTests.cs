using System.Linq.Expressions;
using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using E3A.Application.Engineers.RelistEngineer;
using E3A.Application.Exceptions;
using E3A.Application.Publishing.RegenerateMarketplace;
using E3A.Domain.Engineers;
using E3A.Domain.Publishing;
using E3A.Tests.Engineers.Shared;
using E3A.Tests.Publishing.Shared;
using FluentAssertions;
using MediatR;
using NSubstitute;
using Xunit;

namespace E3A.Tests.Engineers.RelistEngineer;

public sealed class RelistEngineerHandlerTests
{
    private readonly IEngineerRepository _engineerRepository = Substitute.For<IEngineerRepository>();
    private readonly IItemVersionRepository _itemVersionRepository = Substitute.For<IItemVersionRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly Guid _ownerUserId = Guid.NewGuid();
    private readonly Engineer _engineer;
    private readonly RelistEngineerHandler _sut;

    public RelistEngineerHandlerTests()
    {
        _engineer = EngineerFactory.Unlisted(_ownerUserId);
        _currentUserService.UserId.Returns(_ownerUserId);
        _engineerRepository.GetByIdAsync(_engineer.Id, Arg.Any<CancellationToken>()).Returns(_engineer);
        _sut = new RelistEngineerHandler(_engineerRepository, _itemVersionRepository, _currentUserService, _sender);
    }

    [Fact]
    public async Task Handle_ShouldRelistAndRegenerateMarketplace_WhenEngineerIsUnlisted()
    {
        var result = await _sut.Handle(new RelistEngineerCommand(_engineer.Id), CancellationToken.None);

        result.Status.Should().Be(nameof(EngineerStatus.Published));
        _engineer.Status.Should().Be(EngineerStatus.Published);
        await _engineerRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _sender.Received(1).Send(Arg.Any<RegenerateMarketplaceCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRuleViolation_WhenEngineerIsNotUnlisted()
    {
        _engineerRepository.GetByIdAsync(_engineer.Id, Arg.Any<CancellationToken>()).Returns(EngineerFactory.Published(_ownerUserId));

        await Act().Should().ThrowAsync<BusinessRuleViolationCoreException>().Where(x => x.ErrorCode == ErrorCodes.EngineerNotUnlisted);
        await AssertNothingCommitted();
    }

    [Fact]
    public async Task Handle_ShouldThrowConflict_WhenAPublishIsRunning()
    {
        _itemVersionRepository
            .FirstOrDefaultAsync(Arg.Any<Expression<Func<ItemVersion, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<ItemVersion>, IQueryable<ItemVersion>>?>(), Arg.Any<Func<IQueryable<ItemVersion>, IOrderedQueryable<ItemVersion>>?>(), Arg.Any<bool>())
            .Returns(ItemVersionFactory.Building(_engineer.Id));

        await Act().Should().ThrowAsync<ConflictCoreException>().Where(x => x.ErrorCode == ErrorCodes.PublishAlreadyInProgress);
        await AssertNothingCommitted();
    }

    private Func<Task> Act() => async () => await _sut.Handle(new RelistEngineerCommand(_engineer.Id), CancellationToken.None);

    private async Task AssertNothingCommitted()
    {
        await _engineerRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>()).ConfigureAwait(false);
        await _sender.DidNotReceive().Send(Arg.Any<RegenerateMarketplaceCommand>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
    }
}
