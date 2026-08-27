using System.Text.Json;
using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using E3A.Application.Engineers.GetImportManifest;
using E3A.Application.Engineers.Shared;
using E3A.Application.Exceptions;
using E3A.Domain.Engineers;
using E3A.Tests.Engineers.Shared;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace E3A.Tests.Engineers.GetImportManifest;

public sealed class GetImportManifestQueryHandlerTests
{
    private readonly IEngineerRepository _engineerRepository = Substitute.For<IEngineerRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _ownerUserId = Guid.NewGuid();
    private readonly GetImportManifestQueryHandler _sut;

    public GetImportManifestQueryHandlerTests()
    {
        _currentUserService.UserId.Returns(_ownerUserId);
        _sut = new GetImportManifestQueryHandler(_engineerRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorized_WhenUserIsMissing()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = async () => await _sut.Handle(new GetImportManifestQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedCoreException>().Where(x => x.ErrorCode == ErrorCodes.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFound_WhenEngineerDoesNotExist()
    {
        _engineerRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>(), asNoTracking: true).Returns((Engineer?)null);

        var act = async () => await _sut.Handle(new GetImportManifestQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundCoreException>().Where(x => x.ErrorCode == ErrorCodes.EngineerNotFound);
    }

    [Fact]
    public async Task Handle_ShouldThrowForbidden_WhenEngineerIsNotOwned()
    {
        var engineer = EngineerFactory.Draft(Guid.NewGuid());
        _engineerRepository.GetByIdAsync(engineer.Id, Arg.Any<CancellationToken>(), asNoTracking: true).Returns(engineer);

        var act = async () => await _sut.Handle(new GetImportManifestQuery(engineer.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenCoreException>().Where(x => x.ErrorCode == ErrorCodes.EngineerNotOwned);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFound_WhenDraftNotUploaded()
    {
        var engineer = EngineerFactory.Draft(_ownerUserId);
        _engineerRepository.GetByIdAsync(engineer.Id, Arg.Any<CancellationToken>(), asNoTracking: true).Returns(engineer);

        var act = async () => await _sut.Handle(new GetImportManifestQuery(engineer.Id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundCoreException>().Where(x => x.ErrorCode == ErrorCodes.EngineerDraftNotUploaded);
    }

    [Fact]
    public async Task Handle_ShouldReturnManifest_WhenDraftUploaded()
    {
        var engineer = EngineerFactory.Draft(_ownerUserId);
        var manifest = new ImportManifestResult([new ImportedItemResult("agents/a.md", "agents/a.md", ImportCategories.Agents)], [], [], [".env"], [], null, DateTimeOffset.UtcNow);
        engineer.ReplaceDraftManifest(JsonSerializer.Serialize(manifest));
        _engineerRepository.GetByIdAsync(engineer.Id, Arg.Any<CancellationToken>(), asNoTracking: true).Returns(engineer);

        var result = await _sut.Handle(new GetImportManifestQuery(engineer.Id), CancellationToken.None);

        result.Should().BeEquivalentTo(manifest);
    }
}
