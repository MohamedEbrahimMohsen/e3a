using System.Linq.Expressions;
using System.Text;
using Core.Azure.Clients;
using Core.DDD.Models;
using Core.Errors;
using E3A.Application.Exceptions;
using E3A.Application.Options;
using E3A.Application.Publishing.RegenerateMarketplace;
using E3A.Application.Publishing.Shared;
using E3A.Domain.Engineers;
using E3A.Domain.Identity;
using E3A.Domain.Publishing;
using E3A.Domain.Teams;
using E3A.Tests.Engineers.Shared;
using E3A.Tests.Publishing.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace E3A.Tests.Publishing.RegenerateMarketplace;

public sealed class RegenerateMarketplaceHandlerTests
{
    private readonly IEngineerRepository _engineerRepository = Substitute.For<IEngineerRepository>();
    private readonly ITeamRepository _teamRepository = Substitute.For<ITeamRepository>();
    private readonly IItemVersionRepository _itemVersionRepository = Substitute.For<IItemVersionRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IStorageBlobClient _storageBlobClient = Substitute.For<IStorageBlobClient>();
    private readonly AzureOptions _azureOptions = new() { ManagedIdentityClientId = "managed-identity", StorageAccountUrl = "https://e3a.blob.core.windows.net", PublicBlobContainerName = "public" };
    private readonly Guid _ownerUserId = Guid.NewGuid();
    private readonly List<ItemVersion> _publishedVersions = [];
    private string _uploadedJson = string.Empty;

    public RegenerateMarketplaceHandlerTests()
    {
        _itemVersionRepository.FindAsync(Arg.Any<Expression<Func<ItemVersion, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<ItemVersion>, IQueryable<ItemVersion>>?>(), Arg.Any<Func<IQueryable<ItemVersion>, IOrderedQueryable<ItemVersion>>?>(), Arg.Any<bool>()).Returns(_ => _publishedVersions);
        _userRepository.FindAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<User>, IQueryable<User>>?>(), Arg.Any<Func<IQueryable<User>, IOrderedQueryable<User>>?>(), Arg.Any<bool>()).Returns([]);
        _teamRepository.FindPaginatedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Team, bool>>>(), Arg.Any<Func<IQueryable<Team>, IQueryable<Team>>>(), Arg.Any<Func<IQueryable<Team>, IOrderedQueryable<Team>>>(), Arg.Any<bool>()).Returns(new PageData<Team> { Items = [], TotalPages = 0 });
        _storageBlobClient
            .When(x => x.UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), PublishBlobPaths.RootMarketplaceBlobName, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()))
            .Do(call => _uploadedJson = new StreamReader((Stream)call[0]!, Encoding.UTF8).ReadToEnd());
    }

    [Fact]
    public async Task Handle_ShouldWriteMarketplaceWithEveryPublishedEngineer_WhenCalled()
    {
        GivenPage(1, 1, PublishedEngineer("alpha"), PublishedEngineer("beta"));

        await Sut().Handle(new RegenerateMarketplaceCommand(), CancellationToken.None);

        await _storageBlobClient.Received(1).UploadAsync(Arg.Any<Stream>(), _azureOptions.ManagedIdentityClientId, _azureOptions.StorageAccountUrl, _azureOptions.PublicBlobContainerName, PublishBlobPaths.RootMarketplaceBlobName, PublishBlobPaths.MarketplaceContentType, PublishingOptionsFactory.MarketplaceCacheControl, true, Arg.Any<CancellationToken>());
        _uploadedJson.Should().Contain("e3a-alpha").And.Contain("e3a-beta");
    }

    [Fact]
    public async Task Handle_ShouldExcludeUnlistedEngineers_WhenGenerating()
    {
        var listed = PublishedEngineer("alpha");
        var unlisted = PublishedEngineer("hidden");
        unlisted.Unlist();
        GivenPage(1, 1, listed);

        await Sut().Handle(new RegenerateMarketplaceCommand(), CancellationToken.None);

        await _engineerRepository.Received(1).FindPaginatedAsync(1, Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Is<Expression<Func<Engineer, bool>>?>(x => x != null && x.Compile()(listed) && !x.Compile()(unlisted)), Arg.Any<Func<IQueryable<Engineer>, IQueryable<Engineer>>?>(), Arg.Any<Func<IQueryable<Engineer>, IOrderedQueryable<Engineer>>?>(), Arg.Any<bool>());
        _uploadedJson.Should().Contain("e3a-alpha").And.NotContain("e3a-hidden");
    }

    [Fact]
    public async Task Handle_ShouldSkipEngineer_WhenLatestVersionIsNotPublished()
    {
        GivenPage(1, 1, PublishedEngineer("alpha"), PublishedEngineer("beta", versionIsPublished: false));

        await Sut().Handle(new RegenerateMarketplaceCommand(), CancellationToken.None);

        _uploadedJson.Should().Contain("e3a-alpha").And.NotContain("e3a-beta");
    }

    [Fact]
    public async Task Handle_ShouldPageThroughAllEngineers_WhenResultsExceedOnePage()
    {
        GivenPage(1, 2, PublishedEngineer("alpha"));
        GivenPage(2, 2, PublishedEngineer("beta"));

        await Sut().Handle(new RegenerateMarketplaceCommand(), CancellationToken.None);

        await _engineerRepository.Received(1).FindPaginatedAsync(1, Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Engineer, bool>>?>(), Arg.Any<Func<IQueryable<Engineer>, IQueryable<Engineer>>?>(), Arg.Any<Func<IQueryable<Engineer>, IOrderedQueryable<Engineer>>?>(), Arg.Any<bool>());
        await _engineerRepository.Received(1).FindPaginatedAsync(2, Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Engineer, bool>>?>(), Arg.Any<Func<IQueryable<Engineer>, IQueryable<Engineer>>?>(), Arg.Any<Func<IQueryable<Engineer>, IOrderedQueryable<Engineer>>?>(), Arg.Any<bool>());
        _uploadedJson.Should().Contain("e3a-alpha").And.Contain("e3a-beta");
    }

    [Fact]
    public async Task Handle_ShouldThrowInternalServerError_WhenPageCapIsExceeded()
    {
        GivenPage(1, 5, PublishedEngineer("alpha"));

        var act = async () => await Sut(marketplaceMaxPages: 1).Handle(new RegenerateMarketplaceCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<InternalServerErrorCoreException>().Where(x => x.ErrorCode == ErrorCodes.MarketplaceEngineerLimitExceeded);
        await _storageBlobClient.DidNotReceive().UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldFallBackToSlug_WhenOwnerHasNoUserName()
    {
        GivenPage(1, 1, PublishedEngineer("alpha"));
        _userRepository.FindAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<User>, IQueryable<User>>?>(), Arg.Any<Func<IQueryable<User>, IOrderedQueryable<User>>?>(), Arg.Any<bool>()).Returns([new User { Id = _ownerUserId, UserName = null }]);

        await Sut().Handle(new RegenerateMarketplaceCommand(), CancellationToken.None);

        _uploadedJson.Should().Contain("\"name\": \"alpha\"");
    }

    private RegenerateMarketplaceHandler Sut(int marketplaceMaxPages = 50)
    {
        return new RegenerateMarketplaceHandler(_engineerRepository, _teamRepository, _itemVersionRepository, _userRepository, _storageBlobClient, Options.Create(_azureOptions), Options.Create(PublishingOptionsFactory.Default(marketplaceMaxPages: marketplaceMaxPages)));
    }

    private Engineer PublishedEngineer(string slug, bool versionIsPublished = true)
    {
        var engineer = EngineerFactory.Published(_ownerUserId, slug: slug);
        var version = ItemVersionFactory.Published(engineer.Id, zipBlobPath: $"z/e3a-{slug}/1.0.0.zip");
        engineer.MarkPublished(version.Id);

        if (versionIsPublished)
        {
            _publishedVersions.Add(version);
        }

        return engineer;
    }

    private void GivenPage(int pageNumber, long totalPages, params Engineer[] engineers)
    {
        _engineerRepository
            .FindPaginatedAsync(pageNumber, Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Engineer, bool>>?>(), Arg.Any<Func<IQueryable<Engineer>, IQueryable<Engineer>>?>(), Arg.Any<Func<IQueryable<Engineer>, IOrderedQueryable<Engineer>>?>(), Arg.Any<bool>())
            .Returns(new PageData<Engineer> { Items = [.. engineers], PageNumber = pageNumber, PageSize = 100, TotalItems = engineers.Length, TotalPages = totalPages });
    }
}
