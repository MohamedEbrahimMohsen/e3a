using E3A.Application.Options;
using E3A.Application.Publishing.Shared;
using E3A.Domain.Engineers;
using E3A.Tests.Engineers.Shared;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Publishing.Shared;

public sealed class PluginTreeAssemblerTests
{
    private readonly PublishingOptions _options = PublishingOptionsFactory.Default();
    private readonly Engineer _engineer = EngineerFactory.Draft(Guid.NewGuid());

    [Fact]
    public void Assemble_ShouldKeepOnlyManifestTargets_WhenSnapshotHasExtraFiles()
    {
        var snapshotAssets = PluginFileFactory.Files("agents/reviewer.md", "notes/scratch.md");
        var manifest = PluginFileFactory.Manifest("agents/reviewer.md");

        var result = PluginTreeAssembler.Assemble(snapshotAssets, manifest, _engineer, "1.0.0", "mohamed", _options);

        result.Select(x => x.Path).Should().Contain("agents/reviewer.md");
        result.Select(x => x.Path).Should().NotContain("notes/scratch.md");
    }

    [Fact]
    public void Assemble_ShouldIncludeConvertedHouseRulesSkill_WhenManifestConvertsIt()
    {
        var snapshotAssets = PluginFileFactory.Files("skills/house-rules/SKILL.md");
        var manifest = PluginFileFactory.ConvertingManifest("skills/house-rules/SKILL.md");

        var result = PluginTreeAssembler.Assemble(snapshotAssets, manifest, _engineer, "1.0.0", "mohamed", _options);

        result.Select(x => x.Path).Should().Contain("skills/house-rules/SKILL.md");
    }

    [Fact]
    public void Assemble_ShouldAppendPluginJsonAndOrderOrdinally_WhenCalled()
    {
        var snapshotAssets = PluginFileFactory.Files("skills/house-rules/SKILL.md", "agents/reviewer.md", "commands/ship.md");
        var manifest = PluginFileFactory.Manifest("skills/house-rules/SKILL.md", "agents/reviewer.md", "commands/ship.md");

        var result = PluginTreeAssembler.Assemble(snapshotAssets, manifest, _engineer, "1.0.0", "mohamed", _options);

        var paths = result.Select(x => x.Path).ToList();
        var ordinallySorted = paths.OrderBy(x => x, StringComparer.Ordinal).ToList();
        paths.Should().Contain(PluginJsonGenerator.PluginJsonPath);
        paths.Should().Equal(ordinallySorted);
    }
}
