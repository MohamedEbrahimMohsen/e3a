using System.Text.Json;
using Core.Azure.Clients;
using Core.Identity.Tokens.CurrentUser;
using E3A.Application.Engineers.Shared;
using E3A.Application.Engineers.UploadEngineerDraft;
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

public sealed class UploadEngineerDraftHandlerTests
{
    private readonly IEngineerRepository _engineerRepository = Substitute.For<IEngineerRepository>();
    private readonly IItemVersionRepository _itemVersionRepository = Substitute.For<IItemVersionRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IStorageBlobClient _storageBlobClient = Substitute.For<IStorageBlobClient>();
    private readonly IFormFile _file = Substitute.For<IFormFile>();
    private readonly Guid _ownerUserId = Guid.NewGuid();
    private readonly AzureOptions _azureOptions = new() { ManagedIdentityClientId = "managed-identity", StorageAccountUrl = "https://e3a.blob.core.windows.net", DraftsBlobContainerName = "drafts" };
    private readonly Engineer _engineer;
    private readonly UploadEngineerDraftHandler _sut;

    public UploadEngineerDraftHandlerTests()
    {
        _engineer = EngineerFactory.Draft(_ownerUserId);
        _currentUserService.UserId.Returns(_ownerUserId);
        _engineerRepository.GetByIdAsync(_engineer.Id, Arg.Any<CancellationToken>()).Returns(_engineer);
        _file.OpenReadStream().Returns(_ => ZipFixtureFactory.AsStream(ZipFixtureFactory.Build(("skills/a/SKILL.md", "skill body"), ("CLAUDE.md", "house rules"))));
        _sut = new UploadEngineerDraftHandler(_engineerRepository, _itemVersionRepository, _currentUserService, _storageBlobClient, Options.Create(UploadsOptionsFactory.Default()), Options.Create(_azureOptions));
    }

    [Fact]
    public async Task Handle_ShouldDeletePriorAssets_WhenUploadIsValid()
    {
        await _sut.Handle(new UploadEngineerDraftCommand(_engineer.Id, _file), CancellationToken.None);

        await _storageBlobClient.Received(1).DeleteByPrefixAsync(_azureOptions.ManagedIdentityClientId, _azureOptions.StorageAccountUrl, _azureOptions.DraftsBlobContainerName, $"{_ownerUserId}/{_engineer.Id}/", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldUploadNormalizedAssets_WhenUploadIsValid()
    {
        await _sut.Handle(new UploadEngineerDraftCommand(_engineer.Id, _file), CancellationToken.None);

        var blobPrefix = $"{_ownerUserId}/{_engineer.Id}/";
        await _storageBlobClient.Received(1).UploadAsync(Arg.Any<Stream>(), _azureOptions.ManagedIdentityClientId, _azureOptions.StorageAccountUrl, _azureOptions.DraftsBlobContainerName, $"{blobPrefix}skills/a/SKILL.md", Arg.Any<CancellationToken>());
        await _storageBlobClient.Received(1).UploadAsync(Arg.Any<Stream>(), _azureOptions.ManagedIdentityClientId, _azureOptions.StorageAccountUrl, _azureOptions.DraftsBlobContainerName, $"{blobPrefix}skills/house-rules/SKILL.md", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldPersistManifestAndReturnIt_WhenUploadIsValid()
    {
        var before = DateTimeOffset.UtcNow;

        var result = await _sut.Handle(new UploadEngineerDraftCommand(_engineer.Id, _file), CancellationToken.None);

        JsonSerializer.Deserialize<ImportManifestResult>(_engineer.DraftManifestJson!).Should().BeEquivalentTo(result);
        result.UploadedAt.Should().BeOnOrAfter(before);
        _engineerRepository.Received(1).Update(_engineer);
        await _engineerRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
