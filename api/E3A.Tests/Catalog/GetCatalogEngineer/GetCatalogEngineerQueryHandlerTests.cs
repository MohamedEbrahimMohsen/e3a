using Core.Errors;
using E3A.Application.Catalog.GetCatalogEngineer;
using E3A.Application.Exceptions;
using E3A.Domain.Engineers;
using E3A.Tests.Engineers.Shared;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;
using Xunit;

namespace E3A.Tests.Catalog.GetCatalogEngineer;

public sealed class GetCatalogEngineerQueryHandlerTests
{
    private readonly IEngineerRepository _engineerRepository = Substitute.For<IEngineerRepository>();
    private readonly GetCatalogEngineerQueryHandler _sut;

    public GetCatalogEngineerQueryHandlerTests()
    {
        _sut = new GetCatalogEngineerQueryHandler(_engineerRepository);
    }

    [Fact]
    public async Task Handle_ShouldReturnDetail_WhenPublishedEngineerExists()
    {
        var ownerUserId = Guid.NewGuid();
        var engineer = EngineerFactory.Published(ownerUserId, installCount: 4);
        _engineerRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<Engineer, bool>>>(), Arg.Any<CancellationToken>(), asNoTracking: true).Returns(engineer);

        var result = await _sut.Handle(new GetCatalogEngineerQuery(engineer.Slug), CancellationToken.None);

        result.Id.Should().Be(engineer.Id);
        result.Slug.Should().Be(engineer.Slug);
        result.OwnerUserId.Should().Be(ownerUserId);
        result.InstallCount.Should().Be(4);
        result.LatestVersionId.Should().Be(engineer.LatestVersionId);
        result.HookWarnings.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFound_WhenNoPublishedEngineerMatchesSlug()
    {
        _engineerRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<Engineer, bool>>>(), Arg.Any<CancellationToken>(), asNoTracking: true).Returns((Engineer?)null);

        var act = async () => await _sut.Handle(new GetCatalogEngineerQuery("missing-engineer"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundCoreException>().Where(x => x.ErrorCode == ErrorCodes.EngineerNotFound);
    }

    [Fact]
    public async Task Handle_ShouldMatchOnlyPublishedWithSlug_WhenQueryingTheRepository()
    {
        _engineerRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<Engineer, bool>>>(), Arg.Any<CancellationToken>(), asNoTracking: true).Returns((Engineer?)null);

        var act = async () => await _sut.Handle(new GetCatalogEngineerQuery(EngineerFactory.DefaultSlug), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundCoreException>();
        await _engineerRepository.Received(1).FirstOrDefaultAsync(Arg.Is<Expression<Func<Engineer, bool>>>(expression => FilterMatchesOnlyPublishedSlug(expression)), Arg.Any<CancellationToken>(), asNoTracking: true);
    }

    private static bool FilterMatchesOnlyPublishedSlug(Expression<Func<Engineer, bool>> expression)
    {
        var filter = expression.Compile();
        return filter(EngineerFactory.Published(Guid.NewGuid()))
            && !filter(EngineerFactory.Draft(Guid.NewGuid()))
            && !filter(EngineerFactory.Published(Guid.NewGuid(), slug: "another-engineer"));
    }
}
