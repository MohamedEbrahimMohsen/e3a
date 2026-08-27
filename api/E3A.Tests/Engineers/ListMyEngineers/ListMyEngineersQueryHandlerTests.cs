using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using E3A.Application.Engineers.ListMyEngineers;
using E3A.Application.Exceptions;
using E3A.Domain.Engineers;
using E3A.Tests.Engineers.Shared;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;
using Xunit;

namespace E3A.Tests.Engineers.ListMyEngineers;

public sealed class ListMyEngineersQueryHandlerTests
{
    private readonly IEngineerRepository _engineerRepository = Substitute.For<IEngineerRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _ownerUserId = Guid.NewGuid();
    private readonly ListMyEngineersQueryHandler _sut;

    public ListMyEngineersQueryHandlerTests()
    {
        _currentUserService.UserId.Returns(_ownerUserId);
        _sut = new ListMyEngineersQueryHandler(_engineerRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_ShouldReturnOwnedEngineersNewestFirst_WhenEngineersExist()
    {
        var olderEngineer = EngineerFactory.Draft(_ownerUserId, creationDate: DateTimeOffset.UtcNow.AddDays(-2));
        var newerEngineer = EngineerFactory.Draft(_ownerUserId, creationDate: DateTimeOffset.UtcNow.AddDays(-1));
        _engineerRepository.FindAsync(Arg.Any<Expression<Func<Engineer, bool>>>(), Arg.Any<CancellationToken>(), asNoTracking: true).Returns([olderEngineer, newerEngineer]);

        var result = await _sut.Handle(new ListMyEngineersQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Id.Should().Be(newerEngineer.Id);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyList_WhenCallerOwnsNoEngineers()
    {
        _engineerRepository.FindAsync(Arg.Any<Expression<Func<Engineer, bool>>>(), Arg.Any<CancellationToken>(), asNoTracking: true).Returns([]);

        var result = await _sut.Handle(new ListMyEngineersQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorized_WhenUserIsNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = async () => await _sut.Handle(new ListMyEngineersQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedCoreException>().Where(x => x.ErrorCode == ErrorCodes.UserNotAuthenticated);
    }
}
