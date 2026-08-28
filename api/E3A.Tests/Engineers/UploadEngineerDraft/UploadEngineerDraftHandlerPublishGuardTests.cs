using System.Linq.Expressions;
using Core.Azure.Clients;
using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using E3A.Application.Engineers.UploadEngineerDraft;
using E3A.Application.Exceptions;
using E3A.Application.Options;
using E3A.Domain.Engineers;
using E3A.Domain.Publishing;
using E3A.Tests.Engineers.Shared;
using E3A.Tests.Publishing.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace E3A.Tests.Engineers.UploadEngineerDraft;

public sealed class UploadEngineerDraftHandlerPublishGuardTests
{
    private readonly IEngineerRepository _engineerRepository = Substitute.For<IEngineerRepository>();
    private readonly IItemVersionRepository _itemVersionRepository = Substitute.For<IItemVersionRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IStorageBlobClient _storageBlobClient = Substitute.For<IStorageBlobClient>();
    private readonly IFormFile _file = Substitute.For<IFormFile>();
    private readonly Guid _ownerUserId = Guid.NewGuid();
    private readonly AzureOptions _azureOptions = new() { ManagedIdentityClientId = "managed-identity", StorageAccountUrl = "https://e3a.blob.core.windows.net", DraftsBlobContainerName = "drafts" };
    private readonly List<ItemVersion> _versions = [];
    private readonly Engineer _engineer;
    private readonly UploadEngineerDraftHandler _sut;

    public UploadEngineerDraftHandlerPublishGuardTests()
    {
        _engineer = EngineerFactory.Draft(_ownerUserId);
        _currentUserService.UserId.Returns(_ownerUserId);
        _engineerRepository.GetByIdAsync(_engineer.Id, Arg.Any<CancellationToken>()).Returns(_engineer);
        _file.OpenReadStream().Returns(_ => ZipFixtureFactory.AsStream(ZipFixtureFactory.Build(("skills/a/SKILL.md", "skill body"))));
        _itemVersionRepository
            .FirstOrDefaultAsync(Arg.Any<Expression<Func<ItemVersion, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<ItemVersion>, IQueryable<ItemVersion>>?>(), Arg.Any<Func<IQueryable<ItemVersion>, IOrderedQueryable<ItemVersion>>?>(), Arg.Any<bool>())
            .Returns(call => _versions.FirstOrDefault(call.Arg<Expression<Func<ItemVersion, bool>>>().Compile()));
        _sut = new UploadEngineerDraftHandler(_engineerRepository, _itemVersionRepository, _currentUserService, _storageBlobClient, Options.Create(UploadsOptionsFactory.Default()), Options.Create(_azureOptions));
    }

    [Fact]
    public async Task Handle_ShouldThrowConflict_WhenAVersionIsQueued()
    {
        _versions.Add(ItemVersionFactory.Queued(_engineer.Id));

        await Act().Should().ThrowAsync<ConflictCoreException>().Where(x => x.ErrorCode == ErrorCodes.PublishAlreadyInProgress);
        await AssertStorageAndDatabaseUntouched();
    }

    [Fact]
    public async Task Handle_ShouldThrowConflict_WhenAVersionIsBuilding()
    {
        _versions.Add(ItemVersionFactory.Building(_engineer.Id));

        await Act().Should().ThrowAsync<ConflictCoreException>().Where(x => x.ErrorCode == ErrorCodes.PublishAlreadyInProgress);
        await AssertStorageAndDatabaseUntouched();
    }

    [Fact]
    public async Task Handle_ShouldUploadDraft_WhenEngineerHasNoVersions()
    {
        await _sut.Handle(new UploadEngineerDraftCommand(_engineer.Id, _file), CancellationToken.None);

        await _storageBlobClient.Received(1).UploadAsync(Arg.Any<Stream>(), _azureOptions.ManagedIdentityClientId, _azureOptions.StorageAccountUrl, _azureOptions.DraftsBlobContainerName, $"{_ownerUserId}/{_engineer.Id}/skills/a/SKILL.md", Arg.Any<CancellationToken>());
        await _engineerRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldUploadDraft_WhenEveryVersionIsTerminal()
    {
        _versions.Add(ItemVersionFactory.Published(_engineer.Id));
        _versions.Add(ItemVersionFactory.Failed(_engineer.Id, ErrorCodes.PluginNoInstallableContent, versionNumber: 2));

        await _sut.Handle(new UploadEngineerDraftCommand(_engineer.Id, _file), CancellationToken.None);

        await _storageBlobClient.Received(1).UploadAsync(Arg.Any<Stream>(), _azureOptions.ManagedIdentityClientId, _azureOptions.StorageAccountUrl, _azureOptions.DraftsBlobContainerName, $"{_ownerUserId}/{_engineer.Id}/skills/a/SKILL.md", Arg.Any<CancellationToken>());
        await _engineerRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private Func<Task> Act()
    {
        return async () => await _sut.Handle(new UploadEngineerDraftCommand(_engineer.Id, _file), CancellationToken.None);
    }

    private async Task AssertStorageAndDatabaseUntouched()
    {
        await _storageBlobClient.DidNotReceive().DeleteByPrefixAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _storageBlobClient.DidNotReceive().UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _engineerRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
