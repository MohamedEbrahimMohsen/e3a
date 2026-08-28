using E3A.Application.Catalog.GetCatalogTags;
using E3A.Domain.Engineers;
using E3A.Tests.Engineers.Shared;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;
using Xunit;

namespace E3A.Tests.Catalog.GetCatalogTags;

public sealed class GetCatalogTagsQueryHandlerTests
{
    private readonly IEngineerRepository _engineerRepository = Substitute.For<IEngineerRepository>();
    private readonly GetCatalogTagsQueryHandler _sut;

    public GetCatalogTagsQueryHandlerTests()
    {
        _sut = new GetCatalogTagsQueryHandler(_engineerRepository);
    }

    [Fact]
    public async Task Handle_ShouldReturnTagsWithEngineerCounts_WhenPublishedEngineersExist()
    {
        GivePublished(Published("first-engineer", ["dotnet", "ddd"]), Published("second-engineer", ["dotnet", "react"]));

        var result = await _sut.Handle(new GetCatalogTagsQuery(), CancellationToken.None);

        result.Should().Contain(x => x.Tag == "dotnet" && x.Count == 2);
        result.Should().Contain(x => x.Tag == "ddd" && x.Count == 1);
        result.Should().Contain(x => x.Tag == "react" && x.Count == 1);
    }

    [Fact]
    public async Task Handle_ShouldGroupTagsCaseInsensitively_WhenCasingDiffers()
    {
        GivePublished(Published("first-engineer", ["DotNet"]), Published("second-engineer", ["dotnet"]));

        var result = await _sut.Handle(new GetCatalogTagsQuery(), CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Tag.Should().Be("dotnet");
        result[0].Count.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldCountEngineersNotOccurrences_WhenAnEngineerRepeatsATag()
    {
        GivePublished(Published("first-engineer", ["dotnet", "DotNet"]));

        var result = await _sut.Handle(new GetCatalogTagsQuery(), CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Count.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldOrderByCountThenTag_WhenCountsTie()
    {
        GivePublished(Published("first-engineer", ["dotnet", "zeta"]), Published("second-engineer", ["dotnet", "api"]));

        var result = await _sut.Handle(new GetCatalogTagsQuery(), CancellationToken.None);

        result.Select(x => x.Tag).Should().Equal("dotnet", "api", "zeta");
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyList_WhenNothingIsPublished()
    {
        GivePublished();

        var result = await _sut.Handle(new GetCatalogTagsQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    private static Engineer Published(string slug, List<string> tags)
    {
        return EngineerFactory.Published(Guid.NewGuid(), slug: slug, tags: tags);
    }

    private void GivePublished(params Engineer[] engineers)
    {
        _engineerRepository.FindAsync(Arg.Any<Expression<Func<Engineer, bool>>>(), Arg.Any<CancellationToken>(), asNoTracking: true).Returns([.. engineers]);
    }
}
