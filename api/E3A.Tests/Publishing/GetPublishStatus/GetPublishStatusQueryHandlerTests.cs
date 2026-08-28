using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using E3A.Application.Exceptions;
using E3A.Application.Publishing.GetPublishStatus;
using E3A.Domain.Engineers;
using E3A.Domain.Publishing;
using E3A.Tests.Engineers.Shared;
using E3A.Tests.Publishing.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace E3A.Tests.Publishing.GetPublishStatus;

public sealed class GetPublishStatusQueryHandlerTests
{
    private readonly IItemVersionRepository _itemVersionRepository = Substitute.For<IItemVersionRepository>();
    private readonly IEngineerRepository _engineerRepository = Substitute.For<IEngineerRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _ownerUserId = Guid.NewGuid();
    private readonly Engineer _engineer;
    private readonly ItemVersion _version;
    private readonly GetPublishStatusQueryHandler _sut;

    public GetPublishStatusQueryHandlerTests()
    {
        _engineer = EngineerFactory.Published(_ownerUserId);
        _version = ItemVersionFactory.Published(_engineer.Id);
        _currentUserService.UserId.Returns(_ownerUserId);
        _itemVersionRepository.GetByIdAsync(_version.Id, Arg.Any<CancellationToken>(), asNoTracking: true).Returns(_version);
        _engineerRepository.GetByIdAsync(_engineer.Id, Arg.Any<CancellationToken>(), asNoTracking: true).Returns(_engineer);
        _sut = new GetPublishStatusQueryHandler(_itemVersionRepository, _engineerRepository, _currentUserService, Options.Create(PublishingOptionsFactory.Default()));
    }

    [Fact]
    public async Task Handle_ShouldReturnStatus_WhenCallerOwnsTheEngineer()
    {
        var result = await _sut.Handle(new GetPublishStatusQuery(_version.Id), CancellationToken.None);

        result.VersionId.Should().Be(_version.Id);
        result.EngineerId.Should().Be(_engineer.Id);
        result.Status.Should().Be(nameof(ItemVersionStatus.Published));
        result.SemanticVersion.Should().Be(ItemVersionFactory.DefaultSemanticVersion);
        result.ZipUrl.Should().Be($"{PublishingOptionsFactory.PublicSiteUrl}/{ItemVersionFactory.DefaultZipBlobPath}");
    }

    [Fact]
    public async Task Handle_ShouldReturnFailureReason_WhenVersionFailed()
    {
        var failed = ItemVersionFactory.Failed(_engineer.Id, ErrorCodes.PluginNoInstallableContent);
        _itemVersionRepository.GetByIdAsync(failed.Id, Arg.Any<CancellationToken>(), asNoTracking: true).Returns(failed);

        var result = await _sut.Handle(new GetPublishStatusQuery(failed.Id), CancellationToken.None);

        result.Status.Should().Be(nameof(ItemVersionStatus.Failed));
        result.FailureReason.Should().Be(ErrorCodes.PluginNoInstallableContent);
        result.ZipUrl.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorized_WhenUserIsNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        await Act().Should().ThrowAsync<UnauthorizedCoreException>().Where(x => x.ErrorCode == ErrorCodes.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFound_WhenVersionDoesNotExist()
    {
        _itemVersionRepository.GetByIdAsync(_version.Id, Arg.Any<CancellationToken>(), asNoTracking: true).Returns((ItemVersion?)null);

        await Act().Should().ThrowAsync<NotFoundCoreException>().Where(x => x.ErrorCode == ErrorCodes.PublishVersionNotFound);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFound_WhenEngineerDoesNotExist()
    {
        _engineerRepository.GetByIdAsync(_engineer.Id, Arg.Any<CancellationToken>(), asNoTracking: true).Returns((Engineer?)null);

        await Act().Should().ThrowAsync<NotFoundCoreException>().Where(x => x.ErrorCode == ErrorCodes.EngineerNotFound);
    }

    [Fact]
    public async Task Handle_ShouldThrowForbidden_WhenCallerIsNotOwner()
    {
        _currentUserService.UserId.Returns(Guid.NewGuid());

        await Act().Should().ThrowAsync<ForbiddenCoreException>().Where(x => x.ErrorCode == ErrorCodes.EngineerNotOwned);
    }

    private Func<Task> Act() => async () => await _sut.Handle(new GetPublishStatusQuery(_version.Id), CancellationToken.None);
}
