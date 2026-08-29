using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using Core.Azure.Clients;
using Core.DDD.Models;
using E3A.Application.Options;
using E3A.Application.Publishing.RegenerateMarketplace;
using E3A.Application.Publishing.Shared;
using E3A.Domain.Engineers;
using E3A.Domain.Identity;
using E3A.Domain.Publishing;
using E3A.Domain.Teams;
using E3A.Tests.Engineers.Shared;
using E3A.Tests.Publishing.Shared;
using E3A.Tests.Teams.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace E3A.Tests.Publishing.RegenerateMarketplace;

public sealed class RegenerateMarketplaceTeamTests
{
    private readonly IEngineerRepository _engineerRepository = Substitute.For<IEngineerRepository>();
    private readonly ITeamRepository _teamRepository = Substitute.For<ITeamRepository>();
    private readonly IItemVersionRepository _itemVersionRepository = Substitute.For<IItemVersionRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IStorageBlobClient _storageBlobClient = Substitute.For<IStorageBlobClient>();
    private readonly AzureOptions _azureOptions = new() { ManagedIdentityClientId = "managed-identity", StorageAccountUrl = "https://e3a.blob.core.windows.net", PublicBlobContainerName = "public" };
    private readonly Guid _ownerUserId = Guid.NewGuid();
    private readonly List<ItemVersion> _publishedVersions = [];
    private readonly RegenerateMarketplaceHandler _sut;
    private string _uploadedJson = string.Empty;

    public RegenerateMarketplaceTeamTests()
    {
        _itemVersionRepository.FindAsync(Arg.Any<Expression<Func<ItemVersion, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<ItemVersion>, IQueryable<ItemVersion>>?>(), Arg.Any<Func<IQueryable<ItemVersion>, IOrderedQueryable<ItemVersion>>?>(), Arg.Any<bool>()).Returns(_ => _publishedVersions);
        _userRepository.FindAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<User>, IQueryable<User>>?>(), Arg.Any<Func<IQueryable<User>, IOrderedQueryable<User>>?>(), Arg.Any<bool>()).Returns([]);
        _storageBlobClient
            .When(x => x.UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), PublishBlobPaths.RootMarketplaceBlobName, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()))
            .Do(call => _uploadedJson = new StreamReader((Stream)call[0]!, Encoding.UTF8).ReadToEnd());
        _sut = new RegenerateMarketplaceHandler(_engineerRepository, _teamRepository, _itemVersionRepository, _userRepository, _storageBlobClient, Options.Create(_azureOptions), Options.Create(PublishingOptionsFactory.Default()));
    }

    [Fact]
    public async Task Handle_ShouldIncludeEngineersAndTeams_WhenBothArePublished()
    {
        GivenEngineers(PublishedEngineer("alpha"));
        GivenTeams(PublishedTeam("alpha-squad"));

        await _sut.Handle(new RegenerateMarketplaceCommand(), CancellationToken.None);

        PluginNames().Should().Contain(["e3a-alpha", "e3a-team-alpha-squad"]);
    }

    [Fact]
    public async Task Handle_ShouldOrderPluginsOrdinallyByName_WhenBothTypesArePresent()
    {
        GivenEngineers(PublishedEngineer("zeta"), PublishedEngineer("alpha"));
        GivenTeams(PublishedTeam("beta-squad"), PublishedTeam("alpha-squad"));

        await _sut.Handle(new RegenerateMarketplaceCommand(), CancellationToken.None);

        PluginNames().Should().BeInAscendingOrder(StringComparer.Ordinal);
    }

    [Fact]
    public async Task Handle_ShouldUploadOnce_WhenBothTypesArePresent()
    {
        GivenEngineers(PublishedEngineer("alpha"));
        GivenTeams(PublishedTeam("alpha-squad"));

        await _sut.Handle(new RegenerateMarketplaceCommand(), CancellationToken.None);

        await _storageBlobClient.Received(1).UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), _azureOptions.PublicBlobContainerName, PublishBlobPaths.RootMarketplaceBlobName, Arg.Any<string>(), Arg.Any<string>(), true, Arg.Any<CancellationToken>());
    }

    private List<string> PluginNames()
        => [.. JsonSerializer.Deserialize<MarketplaceDocument>(_uploadedJson, JsonSerializerOptions.Web)!.Plugins.Select(x => x.Name)];

    private Engineer PublishedEngineer(string slug)
    {
        var engineer = EngineerFactory.Published(_ownerUserId, slug: slug);
        var version = ItemVersionFactory.Published(engineer.Id, zipBlobPath: $"z/e3a-{slug}/1.0.0.zip");
        engineer.MarkPublished(version.Id);
        _publishedVersions.Add(version);

        return engineer;
    }

    private Team PublishedTeam(string slug)
    {
        var team = TeamFactory.Draft(_ownerUserId, slug: slug);
        var version = ItemVersionFactory.PublishedTeam(team.Id, zipBlobPath: $"z/e3a-team-{slug}/1.0.0.zip");
        team.MarkPublished(version.Id);
        _publishedVersions.Add(version);

        return team;
    }

    private void GivenEngineers(params Engineer[] engineers)
        => _engineerRepository.FindPaginatedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Engineer, bool>>>(), Arg.Any<Func<IQueryable<Engineer>, IQueryable<Engineer>>>(), Arg.Any<Func<IQueryable<Engineer>, IOrderedQueryable<Engineer>>>(), Arg.Any<bool>()).Returns(new PageData<Engineer> { Items = [.. engineers], TotalPages = 1 });

    private void GivenTeams(params Team[] teams)
        => _teamRepository.FindPaginatedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Team, bool>>>(), Arg.Any<Func<IQueryable<Team>, IQueryable<Team>>>(), Arg.Any<Func<IQueryable<Team>, IOrderedQueryable<Team>>>(), Arg.Any<bool>()).Returns(new PageData<Team> { Items = [.. teams], TotalPages = 1 });
}
