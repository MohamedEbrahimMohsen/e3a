using E3A.Application.Options;
using E3A.Application.Publishing.Shared;
using E3A.Tests.Engineers.Shared;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Publishing.Shared;

public sealed class MarketplaceDocumentGeneratorTests
{
    private readonly PublishingOptions _options = PublishingOptionsFactory.Default();

    [Fact]
    public void GeneratePlugin_ShouldBuildArchiveSource_WhenVersionIsPublished()
    {
        var ownerUserId = Guid.NewGuid();
        var engineer = EngineerFactory.Published(ownerUserId, tags: ["dotnet", "ddd"]);
        var version = ItemVersionFactory.Published(engineer.Id, zipBlobPath: "z/e3a-dive-backend-engineer/1.0.0.zip");

        var plugin = MarketplaceDocumentGenerator.GeneratePlugin(engineer, version, "mohamed", _options);

        plugin.Name.Should().Be("e3a-dive-backend-engineer");
        plugin.Source.Source.Should().Be("archive");
        plugin.Source.Url.Should().Be("https://e3a.dev/z/e3a-dive-backend-engineer/1.0.0.zip");
        plugin.Source.Sha256.Should().Be(version.ZipSha256);
        plugin.Keywords.Should().Equal(engineer.Tags);
    }

    [Fact]
    public void Generate_ShouldWrapPluginsWithNameAndOwner_WhenCalled()
    {
        var engineer = EngineerFactory.Published(Guid.NewGuid());
        var plugin = MarketplaceDocumentGenerator.GeneratePlugin(engineer, ItemVersionFactory.Published(engineer.Id), "mohamed", _options);

        var json = MarketplaceDocumentGenerator.Generate([plugin], _options);

        json.Should().Contain("\"name\": \"e3a\"");
        json.Should().Contain("\"owner\"");
        json.Should().Contain("\"url\": \"https://e3a.dev\"");
        json.Should().Contain("\"plugins\"");
        json.Should().Contain("\"e3a-dive-backend-engineer\"");
    }

    [Fact]
    public void Generate_ShouldEmitEmptyPluginsArray_WhenNoneArePublished()
    {
        var json = MarketplaceDocumentGenerator.Generate([], _options);

        json.Should().Contain("\"plugins\": []");
    }
}
