using E3A.Application.Engineers.ListEngineers;
using E3A.Domain.Engineers;
using E3A.Tests.Engineers.Shared;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;
using Xunit;

namespace E3A.Tests.Engineers.ListEngineers;

public sealed class ListEngineersQueryHandlerTests
{
    private readonly IEngineerRepository _engineerRepository = Substitute.For<IEngineerRepository>();
    private readonly ListEngineersQueryHandler _sut;

    public ListEngineersQueryHandlerTests()
    {
        _sut = new ListEngineersQueryHandler(_engineerRepository);
    }

    [Fact]
    public async Task Handle_ShouldReturnPublishedEngineersNewestFirst_WhenTheyExist()
    {
        var older = EngineerFactory.Draft(Guid.NewGuid(), slug: "older-engineer", creationDate: DateTimeOffset.UtcNow.AddDays(-2));
        var newer = EngineerFactory.Draft(Guid.NewGuid(), slug: "newer-engineer", creationDate: DateTimeOffset.UtcNow.AddDays(-1));
        older.MarkPublished(Guid.NewGuid());
        newer.MarkPublished(Guid.NewGuid());
        _engineerRepository.FindAsync(Arg.Any<Expression<Func<Engineer, bool>>>(), Arg.Any<CancellationToken>(), asNoTracking: true).Returns([older, newer]);

        var result = await _sut.Handle(new ListEngineersQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Slug.Should().Be("newer-engineer");
        result[1].Slug.Should().Be("older-engineer");
    }

    [Fact]
    public async Task Handle_ShouldFilterByPublishedStatus_WhenQueryingTheRepository()
    {
        _engineerRepository.FindAsync(Arg.Any<Expression<Func<Engineer, bool>>>(), Arg.Any<CancellationToken>(), asNoTracking: true).Returns([]);

        await _sut.Handle(new ListEngineersQuery(), CancellationToken.None);

        await _engineerRepository.Received(1).FindAsync(Arg.Is<Expression<Func<Engineer, bool>>>(expression => FilterMatchesOnlyPublished(expression)), Arg.Any<CancellationToken>(), asNoTracking: true);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyList_WhenNothingIsPublished()
    {
        _engineerRepository.FindAsync(Arg.Any<Expression<Func<Engineer, bool>>>(), Arg.Any<CancellationToken>(), asNoTracking: true).Returns([]);

        var result = await _sut.Handle(new ListEngineersQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    private static bool FilterMatchesOnlyPublished(Expression<Func<Engineer, bool>> expression)
    {
        var filter = expression.Compile();
        var published = EngineerFactory.Draft(Guid.NewGuid());
        published.MarkPublished(Guid.NewGuid());
        return filter(published) && !filter(EngineerFactory.Draft(Guid.NewGuid()));
    }
}
