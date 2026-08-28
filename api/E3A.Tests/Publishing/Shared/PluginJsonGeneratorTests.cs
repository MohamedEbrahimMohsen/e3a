using System.Text;
using E3A.Application.Options;
using E3A.Application.Publishing.Shared;
using E3A.Tests.Engineers.Shared;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Publishing.Shared;

public sealed class PluginJsonGeneratorTests
{
    private readonly PublishingOptions _options = PublishingOptionsFactory.Default();

    [Fact]
    public void Generate_ShouldEmitPrefixedNameAndAuthor_WhenCalled()
    {
        var engineer = EngineerFactory.Draft(Guid.NewGuid());

        var file = PluginJsonGenerator.Generate(engineer, "1.2.3", "mohamed", _options);

        file.Path.Should().Be(PluginJsonGenerator.PluginJsonPath);
        var json = Encoding.UTF8.GetString(file.Content);
        json.Should().Contain("\"name\": \"e3a-dive-backend-engineer\"");
        json.Should().Contain("\"version\": \"1.2.3\"");
        json.Should().Contain("\"name\": \"mohamed\"");
        json.Should().Contain("\"url\": \"https://e3a.dev/e/dive-backend-engineer\"");
    }

    [Fact]
    public void Generate_ShouldOmitDescription_WhenEngineerHasNone()
    {
        var engineer = EngineerFactory.Published(Guid.NewGuid(), description: null);

        var file = PluginJsonGenerator.Generate(engineer, "1.0.0", "mohamed", _options);

        Encoding.UTF8.GetString(file.Content).Should().NotContain("description");
    }
}
