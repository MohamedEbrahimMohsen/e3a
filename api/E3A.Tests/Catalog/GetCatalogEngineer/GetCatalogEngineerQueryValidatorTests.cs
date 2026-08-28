using E3A.Application.Catalog.GetCatalogEngineer;
using E3A.Application.Exceptions;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Catalog.GetCatalogEngineer;

public sealed class GetCatalogEngineerQueryValidatorTests
{
    private readonly GetCatalogEngineerQueryValidator _sut = new();

    [Fact]
    public void Validate_ShouldPass_WhenQueryIsValid()
    {
        var result = _sut.Validate(new GetCatalogEngineerQuery("dive-backend-engineer"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenSlugIsEmpty()
    {
        var result = _sut.Validate(new GetCatalogEngineerQuery(string.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.CatalogSlugRequired);
    }
}
