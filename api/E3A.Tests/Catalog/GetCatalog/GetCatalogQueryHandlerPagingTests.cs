using E3A.Application.Catalog.GetCatalog;
using E3A.Application.Catalog.Shared;
using E3A.Application.Options;
using E3A.Domain.Engineers;
using E3A.Tests.Engineers.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;
using Xunit;

namespace E3A.Tests.Catalog.GetCatalog;

public sealed class GetCatalogQueryHandlerPagingTests
{
    private readonly IEngineerRepository _engineerRepository = Substitute.For<IEngineerRepository>();
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;
    private readonly GetCatalogQueryHandler _sut;

    public GetCatalogQueryHandlerPagingTests()
    {
        _sut = new GetCatalogQueryHandler(_engineerRepository, Options.Create(new CatalogOptions { DefaultPageSize = 2, MaxPageSize = 50, SearchTextMaxLength = 100, MaxTagFilters = 10, TagFilterMaxLength = 30 }));
    }

    [Fact]
    public async Task Handle_ShouldOrderByCreationDateDescending_WhenSortIsNewest()
    {
        GivePublished(Published("older-engineer", 0, _now.AddDays(-2)), Published("newer-engineer", 0, _now.AddDays(-1)));

        var result = await _sut.Handle(new GetCatalogQuery(null, [], CatalogSort.Newest), CancellationToken.None);

        result.Items.Select(x => x.Slug).Should().Equal("newer-engineer", "older-engineer");
    }

    [Fact]
    public async Task Handle_ShouldOrderByInstallCountDescending_WhenSortIsMostInstalled()
    {
        GivePublished(Published("rarely-installed", 1, _now), Published("often-installed", 5, _now));

        var result = await _sut.Handle(new GetCatalogQuery(null, []), CancellationToken.None);

        result.Items.Select(x => x.Slug).Should().Equal("often-installed", "rarely-installed");
    }

    [Fact]
    public async Task Handle_ShouldBreakInstallCountTiesByCreationDate_WhenSortIsMostInstalled()
    {
        GivePublished(Published("older-engineer", 5, _now.AddDays(-2)), Published("newer-engineer", 5, _now.AddDays(-1)));

        var result = await _sut.Handle(new GetCatalogQuery(null, []), CancellationToken.None);

        result.Items.Select(x => x.Slug).Should().Equal("newer-engineer", "older-engineer");
    }

    [Fact]
    public async Task Handle_ShouldReturnRequestedPage_WhenPageNumberBeyondFirst()
    {
        GivePublished(Published("first-engineer", 3, _now), Published("second-engineer", 2, _now), Published("third-engineer", 1, _now));

        var result = await _sut.Handle(new GetCatalogQuery(null, [], CatalogSort.MostInstalled, 2, 2), CancellationToken.None);

        result.Items.Should().ContainSingle().Which.Slug.Should().Be("third-engineer");
        result.TotalItems.Should().Be(3);
        result.TotalPages.Should().Be(2);
        result.PageNumber.Should().Be(2);
        result.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldUseDefaultPageSize_WhenPageSizeIsNull()
    {
        GivePublished(Published("first-engineer", 3, _now), Published("second-engineer", 2, _now), Published("third-engineer", 1, _now));

        var result = await _sut.Handle(new GetCatalogQuery(null, []), CancellationToken.None);

        result.Items.Should().HaveCount(2);
        result.PageSize.Should().Be(2);
    }

    private static Engineer Published(string slug, int installCount, DateTimeOffset creationDate)
    {
        return EngineerFactory.Published(Guid.NewGuid(), slug: slug, installCount: installCount, creationDate: creationDate);
    }

    private void GivePublished(params Engineer[] engineers)
    {
        _engineerRepository.FindAsync(Arg.Any<Expression<Func<Engineer, bool>>>(), Arg.Any<CancellationToken>(), asNoTracking: true).Returns([.. engineers]);
    }
}
