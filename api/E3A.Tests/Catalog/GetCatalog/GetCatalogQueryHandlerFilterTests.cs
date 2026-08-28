using E3A.Application.Catalog.GetCatalog;
using E3A.Application.Options;
using E3A.Domain.Engineers;
using E3A.Tests.Engineers.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;
using Xunit;

namespace E3A.Tests.Catalog.GetCatalog;

public sealed class GetCatalogQueryHandlerFilterTests
{
    private readonly IEngineerRepository _engineerRepository = Substitute.For<IEngineerRepository>();
    private readonly Engineer _backendEngineer = EngineerFactory.Published(Guid.NewGuid(), description: "Vertical slices and clean error contracts.");
    private readonly Engineer _frontendEngineer = EngineerFactory.Published(Guid.NewGuid(), slug: "react-engineer", displayName: "React Engineer", description: "Handles graphql work.", tags: ["typescript"]);
    private readonly GetCatalogQueryHandler _sut;

    public GetCatalogQueryHandlerFilterTests()
    {
        _sut = new GetCatalogQueryHandler(_engineerRepository, Options.Create(new CatalogOptions { DefaultPageSize = 2, MaxPageSize = 50, SearchTextMaxLength = 100, MaxTagFilters = 10, TagFilterMaxLength = 30 }));
        _engineerRepository.FindAsync(Arg.Any<Expression<Func<Engineer, bool>>>(), Arg.Any<CancellationToken>(), asNoTracking: true).Returns([_backendEngineer, _frontendEngineer]);
    }

    [Fact]
    public async Task Handle_ShouldQueryOnlyPublished_WhenCalled()
    {
        await _sut.Handle(new GetCatalogQuery(null, []), CancellationToken.None);

        await _engineerRepository.Received(1).FindAsync(Arg.Is<Expression<Func<Engineer, bool>>>(expression => FilterMatchesOnlyPublished(expression)), Arg.Any<CancellationToken>(), asNoTracking: true);
    }

    [Fact]
    public async Task Handle_ShouldReturnAllPublishedMapped_WhenNoFiltersProvided()
    {
        var result = await _sut.Handle(new GetCatalogQuery(null, []), CancellationToken.None);

        result.Items.Should().HaveCount(2);
        result.Items.Single(x => x.Id == _backendEngineer.Id).Should().BeEquivalentTo(new { _backendEngineer.Id, _backendEngineer.Slug, _backendEngineer.DisplayName, _backendEngineer.Description, _backendEngineer.Tags, _backendEngineer.InstallCount, _backendEngineer.LatestVersionId, CreatedAt = _backendEngineer.CreationDate, UpdatedAt = _backendEngineer.UpdationDate });
    }

    [Fact]
    public async Task Handle_ShouldFilterBySearchText_WhenItMatchesDisplayName()
    {
        var result = await _sut.Handle(new GetCatalogQuery("BACKEND", []), CancellationToken.None);

        result.Items.Should().ContainSingle().Which.Slug.Should().Be(EngineerFactory.DefaultSlug);
    }

    [Fact]
    public async Task Handle_ShouldFilterBySearchText_WhenItMatchesDescription()
    {
        var result = await _sut.Handle(new GetCatalogQuery("graphql work", []), CancellationToken.None);

        result.Items.Should().ContainSingle().Which.Slug.Should().Be("react-engineer");
    }

    [Fact]
    public async Task Handle_ShouldFilterBySearchText_WhenItMatchesTags()
    {
        var result = await _sut.Handle(new GetCatalogQuery("typescript", []), CancellationToken.None);

        result.Items.Should().ContainSingle().Which.Slug.Should().Be("react-engineer");
    }

    [Fact]
    public async Task Handle_ShouldTrimSearchText_WhenItHasSurroundingWhitespace()
    {
        var result = await _sut.Handle(new GetCatalogQuery("  backend  ", []), CancellationToken.None);

        result.Items.Should().ContainSingle().Which.Slug.Should().Be(EngineerFactory.DefaultSlug);
    }

    [Fact]
    public async Task Handle_ShouldFilterByTags_WhenAnyTagMatchesCaseInsensitively()
    {
        var result = await _sut.Handle(new GetCatalogQuery(null, ["DOTNET", "sql"]), CancellationToken.None);

        result.Items.Should().ContainSingle().Which.Slug.Should().Be(EngineerFactory.DefaultSlug);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyPage_WhenNothingMatches()
    {
        var result = await _sut.Handle(new GetCatalogQuery("kubernetes", []), CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.TotalItems.Should().Be(0);
        result.TotalPages.Should().Be(0);
    }

    private static bool FilterMatchesOnlyPublished(Expression<Func<Engineer, bool>> expression)
    {
        var filter = expression.Compile();
        return filter(EngineerFactory.Published(Guid.NewGuid())) && !filter(EngineerFactory.Draft(Guid.NewGuid()));
    }
}
