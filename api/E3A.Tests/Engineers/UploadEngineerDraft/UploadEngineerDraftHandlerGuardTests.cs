using System.Text;
using Core.Azure.Clients;
using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using E3A.Application.Engineers.UploadEngineerDraft;
using E3A.Application.Exceptions;
using E3A.Application.Options;
using E3A.Domain.Engineers;
using E3A.Domain.Publishing;
using E3A.Tests.Engineers.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace E3A.Tests.Engineers.UploadEngineerDraft;

public sealed class UploadEngineerDraftHandlerGuardTests
{
    private readonly IEngineerRepository _engineerRepository = Substitute.For<IEngineerRepository>();
    private readonly IItemVersionRepository _itemVersionRepository = Substitute.For<IItemVersionRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IStorageBlobClient _storageBlobClient = Substitute.For<IStorageBlobClient>();
    private readonly IFormFile _file = Substitute.For<IFormFile>();
    private readonly Guid _ownerUserId = Guid.NewGuid();
    private readonly AzureOptions _azureOptions = new() { ManagedIdentityClientId = "managed-identity", StorageAccountUrl = "https://e3a.blob.core.windows.net", DraftsBlobContainerName = "drafts" };
    private readonly UploadEngineerDraftHandler _sut;

    public UploadEngineerDraftHandlerGuardTests()
    {
        _currentUserService.UserId.Returns(_ownerUserId);
        _file.OpenReadStream().Returns(_ => ZipFixtureFactory.AsStream(Encoding.UTF8.GetBytes("this is not a zip archive")));
        _sut = new UploadEngineerDraftHandler(_engineerRepository, _itemVersionRepository, _currentUserService, _storageBlobClient, Options.Create(UploadsOptionsFactory.Default()), Options.Create(_azureOptions));
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorized_WhenUserIsMissing()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = async () => await _sut.Handle(new UploadEngineerDraftCommand(Guid.NewGuid(), _file), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedCoreException>().Where(x => x.ErrorCode == ErrorCodes.UserNotAuthenticated);
        await _engineerRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFound_WhenEngineerDoesNotExist()
    {
        _engineerRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Engineer?)null);

        var act = async () => await _sut.Handle(new UploadEngineerDraftCommand(Guid.NewGuid(), _file), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundCoreException>().Where(x => x.ErrorCode == ErrorCodes.EngineerNotFound);
        await _engineerRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowForbidden_WhenEngineerIsNotOwned()
    {
        var engineer = EngineerFactory.Draft(Guid.NewGuid());
        _engineerRepository.GetByIdAsync(engineer.Id, Arg.Any<CancellationToken>()).Returns(engineer);

        var act = async () => await _sut.Handle(new UploadEngineerDraftCommand(engineer.Id, _file), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenCoreException>().Where(x => x.ErrorCode == ErrorCodes.EngineerNotOwned);
        await _engineerRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldNotTouchBlobOrSave_WhenZipIsInvalid()
    {
        var engineer = EngineerFactory.Draft(_ownerUserId);
        _engineerRepository.GetByIdAsync(engineer.Id, Arg.Any<CancellationToken>()).Returns(engineer);

        var act = async () => await _sut.Handle(new UploadEngineerDraftCommand(engineer.Id, _file), CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestCoreException>().Where(x => x.ErrorCode == ErrorCodes.UploadZipInvalid);
        await _storageBlobClient.DidNotReceive().DeleteByPrefixAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _storageBlobClient.DidNotReceive().UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _engineerRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
