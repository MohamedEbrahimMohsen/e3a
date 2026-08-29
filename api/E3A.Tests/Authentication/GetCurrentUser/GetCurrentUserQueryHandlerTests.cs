using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using E3A.Application.Authentication.GetCurrentUser;
using E3A.Application.Exceptions;
using E3A.Domain.Identity;
using E3A.Tests.Identity.Shared;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace E3A.Tests.Authentication.GetCurrentUser;

public sealed class GetCurrentUserQueryHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly GetCurrentUserQueryHandler _sut;

    public GetCurrentUserQueryHandlerTests()
    {
        _sut = new GetCurrentUserQueryHandler(_userRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_ShouldReturnTheProfile_WhenTheUserExists()
    {
        var user = UserFactory.GitHub();
        _currentUserService.UserId.Returns(user.Id);
        _userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>(), asNoTracking: true).Returns(user);

        var result = await _sut.Handle(new GetCurrentUserQuery(), CancellationToken.None);

        result.Id.Should().Be(user.Id);
        result.GitHubId.Should().Be(user.GitHubId);
        result.GitHubLogin.Should().Be(user.GitHubLogin);
        result.DisplayName.Should().Be(user.DisplayName);
        result.AvatarUrl.Should().Be(user.AvatarUrl);
        result.CreatedAt.Should().Be(user.CreationDate);
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorized_WhenThereIsNoCurrentUser()
    {
        _currentUserService.UserId.Returns((Guid?)null, Guid.Empty);

        var missingUser = async () => await _sut.Handle(new GetCurrentUserQuery(), CancellationToken.None);
        var emptyUser = async () => await _sut.Handle(new GetCurrentUserQuery(), CancellationToken.None);

        await missingUser.Should().ThrowAsync<UnauthorizedCoreException>().Where(x => x.ErrorCode == ErrorCodes.UserNotAuthenticated);
        await emptyUser.Should().ThrowAsync<UnauthorizedCoreException>().Where(x => x.ErrorCode == ErrorCodes.UserNotAuthenticated);
        await _userRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<User>, IQueryable<User>>?>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFound_WhenTheUserRowIsMissing()
    {
        _currentUserService.UserId.Returns(Guid.NewGuid());
        _userRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>(), asNoTracking: true).Returns((User?)null);

        var act = async () => await _sut.Handle(new GetCurrentUserQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundCoreException>().Where(x => x.ErrorCode == ErrorCodes.UserNotFound);
    }
}
