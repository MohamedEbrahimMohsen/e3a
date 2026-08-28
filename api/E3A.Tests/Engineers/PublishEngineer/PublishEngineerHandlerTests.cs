using System.Linq.Expressions;
using Core.Identity.Tokens.CurrentUser;
using E3A.Application.Engineers.PublishEngineer;
using E3A.Domain.Engineers;
using E3A.Domain.Publishing;
using E3A.Tests.Engineers.Shared;
using E3A.Tests.Publishing.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace E3A.Tests.Engineers.PublishEngineer;

public sealed class PublishEngineerHandlerTests
{
    private readonly IEngineerRepository _engineerRepository = Substitute.For<IEngineerRepository>();
    private readonly IItemVersionRepository _itemVersionRepository = Substitute.For<IItemVersionRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _ownerUserId = Guid.NewGuid();
    private readonly Engineer _engineer;
    private readonly PublishEngineerHandler _sut;

    public PublishEngineerHandlerTests()
    {
        _engineer = EngineerFactory.Draft(_ownerUserId);
        _engineer.ReplaceDraftManifest("{\"imported\":[]}");
        _currentUserService.UserId.Returns(_ownerUserId);
        _engineerRepository.GetByIdAsync(_engineer.Id, Arg.Any<CancellationToken>()).Returns(_engineer);
        _sut = new PublishEngineerHandler(_engineerRepository, _itemVersionRepository, _currentUserService, Options.Create(PublishingOptionsFactory.Default()));
    }

    [Fact]
    public async Task Handle_ShouldCreateQueuedVersion_WhenFirstPublish()
    {
        var result = await _sut.Handle(new PublishEngineerCommand(_engineer.Id, VersionIncrement.Patch), CancellationToken.None);

        result.Status.Should().Be("Queued");
        result.SemanticVersion.Should().Be("1.0.0");
        result.VersionNumber.Should().Be(1);
        await _itemVersionRepository.Received(1).AddAsync(Arg.Any<ItemVersion>(), Arg.Any<CancellationToken>());
        await _itemVersionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(VersionIncrement.Patch, "2.5.8")]
    [InlineData(VersionIncrement.Minor, "2.6.0")]
    [InlineData(VersionIncrement.Major, "3.0.0")]
    public async Task Handle_ShouldIncrementFromLatestVersion_WhenPreviousExists(VersionIncrement increment, string expectedSemanticVersion)
    {
        GivenLatestVersion(ItemVersionFactory.Published(_engineer.Id, versionNumber: 4, semanticVersion: "2.5.7"));

        var result = await _sut.Handle(new PublishEngineerCommand(_engineer.Id, increment), CancellationToken.None);

        result.SemanticVersion.Should().Be(expectedSemanticVersion);
        result.VersionNumber.Should().Be(5);
    }

    [Fact]
    public async Task Handle_ShouldFreezeDraftManifest_WhenCreatingVersion()
    {
        await _sut.Handle(new PublishEngineerCommand(_engineer.Id, VersionIncrement.Patch), CancellationToken.None);

        await _itemVersionRepository.Received(1).AddAsync(Arg.Is<ItemVersion>(x => x.FrozenManifestJson == _engineer.DraftManifestJson), Arg.Any<CancellationToken>());
    }

    private void GivenLatestVersion(ItemVersion latest)
    {
        _itemVersionRepository
            .FirstOrDefaultAsync(
                Arg.Any<Expression<Func<ItemVersion, bool>>>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<Func<IQueryable<ItemVersion>, IQueryable<ItemVersion>>?>(),
                Arg.Is<Func<IQueryable<ItemVersion>, IOrderedQueryable<ItemVersion>>?>(x => x != null),
                Arg.Any<bool>())
            .Returns(latest);
    }
}
