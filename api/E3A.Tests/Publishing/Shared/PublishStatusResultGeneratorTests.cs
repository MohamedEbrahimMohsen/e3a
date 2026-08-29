using E3A.Application.Options;
using E3A.Application.Publishing.Shared;
using E3A.Domain.Publishing;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Publishing.Shared;

public sealed class PublishStatusResultGeneratorTests
{
    private readonly PublishingOptions _options = PublishingOptionsFactory.Default();
    private readonly Guid _engineerId = Guid.NewGuid();

    [Fact]
    public void Generate_ShouldBuildAbsoluteZipUrl_WhenVersionIsPublished()
    {
        var version = ItemVersionFactory.Published(_engineerId);

        var result = PublishStatusResultGenerator.Generate(version, _options);

        result.ZipUrl.Should().Be("https://e3a.dev/z/e3a-dive-backend-engineer/1.0.0.zip");
        result.Status.Should().Be("Published");
        result.UpdatedAt.Should().Be(version.UpdationDate);
        result.ItemId.Should().Be(_engineerId);
        result.ItemType.Should().Be(nameof(ItemType.Engineer));
    }

    [Fact]
    public void Generate_ShouldReturnNullZipUrl_WhenVersionHasNoZip()
    {
        var version = ItemVersionFactory.Queued(_engineerId);

        var result = PublishStatusResultGenerator.Generate(version, _options);

        result.ZipUrl.Should().BeNull();
        result.Status.Should().Be("Queued");
    }
}
