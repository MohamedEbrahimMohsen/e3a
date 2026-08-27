using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using E3A.Application.Engineers.GetEngineer;
using E3A.Application.Exceptions;
using E3A.Domain.Engineers;
using E3A.Tests.Engineers.Shared;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace E3A.Tests.Engineers.GetEngineer;

public sealed class GetEngineerQueryHandlerTests
{
    private readonly IEngineerRepository _engineerRepository = Substitute.For<IEngineerRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _ownerUserId = Guid.NewGuid();
    private readonly GetEngineerQueryHandler _sut;

    public GetEngineerQueryHandlerTests()
    {
        _currentUserService.UserId.Returns(_ownerUserId);
        _sut = new GetEngineerQueryHandler(_engineerRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_ShouldReturnDraftEngineer_WhenCallerIsOwner()
    {
        var engineer = EngineerFactory.Draft(_ownerUserId);
        _engineerRepository.GetByIdAsync(engineer.Id, Arg.Any<CancellationToken>(), asNoTracking: true).Returns(engineer);

        var result = await _sut.Handle(new GetEngineerQuery(engineer.Id), CancellationToken.None);

        result.Id.Should().Be(engineer.Id);
        result.Slug.Should().Be(engineer.Slug);
        result.DisplayName.Should().Be(engineer.DisplayName);
        result.Status.Should().Be(nameof(EngineerStatus.Draft));
        result.CreatedAt.Should().Be(engineer.CreationDate);
        result.UpdatedAt.Should().Be(engineer.UpdationDate);
    }

    [Fact]
    public async Task Handle_ShouldReturnPublishedEngineer_WhenCallerIsAnonymous()
    {
        _currentUserService.UserId.Returns((Guid?)null);
        var engineer = EngineerFactory.Draft(Guid.NewGuid());
        engineer.MarkPublished(Guid.NewGuid());
        _engineerRepository.GetByIdAsync(engineer.Id, Arg.Any<CancellationToken>(), asNoTracking: true).Returns(engineer);

        var result = await _sut.Handle(new GetEngineerQuery(engineer.Id), CancellationToken.None);

        result.Id.Should().Be(engineer.Id);
        result.Status.Should().Be(nameof(EngineerStatus.Published));
    }

    [Fact]
    public async Task Handle_ShouldReturnPublishedEngineer_WhenCallerIsNotOwner()
    {
        var engineer = EngineerFactory.Draft(Guid.NewGuid());
        engineer.MarkPublished(Guid.NewGuid());
        _engineerRepository.GetByIdAsync(engineer.Id, Arg.Any<CancellationToken>(), asNoTracking: true).Returns(engineer);

        var result = await _sut.Handle(new GetEngineerQuery(engineer.Id), CancellationToken.None);

        result.Id.Should().Be(engineer.Id);
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorized_WhenCallerIsAnonymousAndEngineerIsNotPublished()
    {
        _currentUserService.UserId.Returns((Guid?)null);
        var engineer = EngineerFactory.Draft(Guid.NewGuid());
        _engineerRepository.GetByIdAsync(engineer.Id, Arg.Any<CancellationToken>(), asNoTracking: true).Returns(engineer);

        var act = async () => await _sut.Handle(new GetEngineerQuery(engineer.Id), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedCoreException>().Where(x => x.ErrorCode == ErrorCodes.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFound_WhenEngineerDoesNotExist()
    {
        _engineerRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>(), asNoTracking: true).Returns((Engineer?)null);

        var act = async () => await _sut.Handle(new GetEngineerQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundCoreException>().Where(x => x.ErrorCode == ErrorCodes.EngineerNotFound);
    }

    [Fact]
    public async Task Handle_ShouldThrowForbidden_WhenCallerIsNotOwnerAndEngineerIsNotPublished()
    {
        var engineer = EngineerFactory.Draft(Guid.NewGuid());
        _engineerRepository.GetByIdAsync(engineer.Id, Arg.Any<CancellationToken>(), asNoTracking: true).Returns(engineer);

        var act = async () => await _sut.Handle(new GetEngineerQuery(engineer.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenCoreException>().Where(x => x.ErrorCode == ErrorCodes.EngineerNotOwned);
    }
}
