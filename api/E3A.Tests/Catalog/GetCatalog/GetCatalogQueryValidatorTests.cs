using E3A.Application.Catalog.GetCatalog;
using E3A.Application.Catalog.Shared;
using E3A.Application.Exceptions;
using E3A.Application.Options;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace E3A.Tests.Catalog.GetCatalog;

public sealed class GetCatalogQueryValidatorTests
{
    private readonly GetCatalogQueryValidator _sut = new(Options.Create(new CatalogOptions { DefaultPageSize = 9, MaxPageSize = 50, SearchTextMaxLength = 100, MaxTagFilters = 10, TagFilterMaxLength = 30 }));

    [Fact]
    public void Validate_ShouldPass_WhenQueryIsValid()
    {
        var result = _sut.Validate(new GetCatalogQuery("backend", ["dotnet"], CatalogSort.Newest, 1, 9));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldPass_WhenPageSizeIsNull()
    {
        var result = _sut.Validate(new GetCatalogQuery(null, []));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenSearchTextExceedsMaxLength()
    {
        var result = _sut.Validate(new GetCatalogQuery(new string('a', 101), []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.CatalogSearchTextTooLong);
    }

    [Fact]
    public void Validate_ShouldFail_WhenTagFiltersExceedMaxCount()
    {
        var tags = Enumerable.Range(0, 11).Select(index => $"tag-{index}").ToList();

        var result = _sut.Validate(new GetCatalogQuery(null, tags));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.CatalogTooManyTagFilters);
    }

    [Fact]
    public void Validate_ShouldFail_WhenTagFilterExceedsMaxLength()
    {
        var result = _sut.Validate(new GetCatalogQuery(null, [new string('a', 31)]));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.CatalogTagFilterTooLong);
    }

    [Fact]
    public void Validate_ShouldFail_WhenSortIsNotDefined()
    {
        var result = _sut.Validate(new GetCatalogQuery(null, [], (CatalogSort)99));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.CatalogSortInvalid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_ShouldFail_WhenPageNumberIsNotPositive(int pageNumber)
    {
        var result = _sut.Validate(new GetCatalogQuery(null, [], CatalogSort.MostInstalled, pageNumber));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.CatalogPageNumberInvalid);
    }

    [Fact]
    public void Validate_ShouldFail_WhenPageSizeIsNotPositive()
    {
        var result = _sut.Validate(new GetCatalogQuery(null, [], CatalogSort.MostInstalled, 1, 0));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.CatalogPageSizeInvalid);
    }

    [Fact]
    public void Validate_ShouldFail_WhenPageSizeExceedsMax()
    {
        var result = _sut.Validate(new GetCatalogQuery(null, [], CatalogSort.MostInstalled, 1, 51));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.CatalogPageSizeInvalid);
    }
}
