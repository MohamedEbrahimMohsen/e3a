using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using E3A.Application.Engineers.DeleteEngineer;
using E3A.Application.Exceptions;
using E3A.Domain.Engineers;
using E3A.Tests.Engineers.Shared;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace E3A.Tests.Engineers.DeleteEngineer;

public sealed class DeleteEngineerHandlerTests
{
    private readonly IEngineerRepository _engineerRepository = Substitute.For<IEngineerRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _ownerUserId = Guid.NewGuid();
    private readonly DeleteEngineerHandler _sut;

    public DeleteEngineerHandlerTests()
    {
        _currentUserService.UserId.Returns(_ownerUserId);
        _sut = new DeleteEngineerHandler(_engineerRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_ShouldSoftDeleteEngineer_WhenCallerIsOwner()
    {
        var engineer = EngineerFactory.Draft(_ownerUserId);
        _engineerRepository.GetByIdAsync(engineer.Id, Arg.Any<CancellationToken>()).Returns(engineer);

        await _sut.Handle(new DeleteEngineerCommand(engineer.Id), CancellationToken.None);

        engineer.IsDeleted.Should().BeTrue();
        engineer.Status.Should().Be(EngineerStatus.Deleted);
        _engineerRepository.Received(1).Update(engineer);
        await _engineerRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        _engineerRepository.DidNotReceive().Delete(Arg.Any<Engineer>());
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorized_WhenUserIsNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = async () => await _sut.Handle(new DeleteEngineerCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedCoreException>().Where(x => x.ErrorCode == ErrorCodes.UserNotAuthenticated);
        await _engineerRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFound_WhenEngineerDoesNotExist()
    {
        _engineerRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Engineer?)null);

        var act = async () => await _sut.Handle(new DeleteEngineerCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundCoreException>().Where(x => x.ErrorCode == ErrorCodes.EngineerNotFound);
        await _engineerRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowForbidden_WhenCallerIsNotOwner()
    {
        var engineer = EngineerFactory.Draft(Guid.NewGuid());
        _engineerRepository.GetByIdAsync(engineer.Id, Arg.Any<CancellationToken>()).Returns(engineer);

        var act = async () => await _sut.Handle(new DeleteEngineerCommand(engineer.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenCoreException>().Where(x => x.ErrorCode == ErrorCodes.EngineerNotOwned);
        _engineerRepository.DidNotReceive().Update(Arg.Any<Engineer>());
        await _engineerRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
