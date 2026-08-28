using E3A.Application.Catalog.Shared;
using E3A.Application.Engineers.Shared;
using E3A.Tests.Engineers.Shared;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace E3A.Tests.Catalog.Shared;

public sealed class CatalogEngineerResultGeneratorTests
{
    [Fact]
    public void GenerateDetail_ShouldReturnEmptyHookWarnings_WhenDraftManifestIsNull()
    {
        var ownerUserId = Guid.NewGuid();
        var engineer = EngineerFactory.Published(ownerUserId, installCount: 7);

        var result = CatalogEngineerResultGenerator.GenerateDetail(engineer);

        result.HookWarnings.Should().BeEmpty();
        result.OwnerUserId.Should().Be(ownerUserId);
        result.Slug.Should().Be(EngineerFactory.DefaultSlug);
        result.InstallCount.Should().Be(7);
    }

    [Fact]
    public void GenerateDetail_ShouldReturnHookWarnings_WhenDraftManifestContainsThem()
    {
        var engineer = EngineerFactory.Published(Guid.NewGuid());
        var manifest = new ImportManifestResult([], [], [], [], [new HookWarningResult("PreToolUse", "Bash", "check.sh")], null, DateTimeOffset.UtcNow);
        engineer.ReplaceDraftManifest(JsonSerializer.Serialize(manifest));

        var result = CatalogEngineerResultGenerator.GenerateDetail(engineer);

        result.HookWarnings.Should().ContainSingle();
        result.HookWarnings[0].Event.Should().Be("PreToolUse");
        result.HookWarnings[0].Matcher.Should().Be("Bash");
        result.HookWarnings[0].Command.Should().Be("check.sh");
    }
}
