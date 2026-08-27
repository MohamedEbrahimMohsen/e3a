using System.Text;
using Core.Errors;
using E3A.Application.Engineers.Shared;
using E3A.Application.Engineers.UploadEngineerDraft;
using E3A.Application.Exceptions;
using E3A.Tests.Engineers.Shared;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Engineers.UploadEngineerDraft;

public sealed class DraftNormalizerTests
{
    [Theory]
    [InlineData("agents/a.md", ImportCategories.Agents)]
    [InlineData("commands/c.md", ImportCategories.Commands)]
    [InlineData("hooks/h.sh", ImportCategories.Hooks)]
    [InlineData("output-styles/o.md", ImportCategories.OutputStyles)]
    [InlineData("monitors/m.sh", ImportCategories.Monitors)]
    [InlineData("bin/b.sh", ImportCategories.Bin)]
    [InlineData("themes/t.json", ImportCategories.Themes)]
    public void Normalize_ShouldImportRecognizedFolders_WhenPresent(string path, string category)
    {
        var draft = Normalize(path);

        draft.Assets.Select(asset => asset.Path).Should().Equal(path);
        draft.Manifest.Imported.Should().ContainSingle().Which.Should().Be(new ImportedItemResult(path, path, category));
    }

    [Fact]
    public void Normalize_ShouldImportSkillFolder_WhenSkillFileAtRoot()
    {
        var draft = Normalize("skills/a/SKILL.md", "skills/a/reference.md");

        draft.Assets.Select(asset => asset.Path).Should().Equal("skills/a/SKILL.md", "skills/a/reference.md");
        draft.Manifest.Imported.Should().OnlyContain(item => item.Category == ImportCategories.Skills);
    }

    [Fact]
    public void Normalize_ShouldSkipSkillFiles_WhenSkillFileMissing()
    {
        var draft = Normalize("skills/a/notes.md", "agents/a.md");

        draft.Assets.Select(asset => asset.Path).Should().Equal("agents/a.md");
        draft.Manifest.Skipped.Should().ContainSingle().Which.Should().Be(new SkippedItemResult("skills/a/notes.md", DraftNormalizer.SkillMissingSkillFileReason));
    }

    [Fact]
    public void Normalize_ShouldImportRootConfigurationFiles_WhenMcpAndLspPresent()
    {
        var draft = Normalize(".mcp.json", ".lsp.json");

        draft.Manifest.Imported.Should().Equal(
            new ImportedItemResult(".mcp.json", ".mcp.json", ImportCategories.McpServers),
            new ImportedItemResult(".lsp.json", ".lsp.json", ImportCategories.LspServers));
    }

    [Fact]
    public void Normalize_ShouldImportLooseHookScripts_WhenScriptExtensionOutsideRecognizedRoots()
    {
        var draft = Normalize("scripts/check.sh");

        draft.Assets.Select(asset => asset.Path).Should().Equal("scripts/check.sh");
        draft.Manifest.Imported.Should().ContainSingle().Which.Category.Should().Be(ImportCategories.HookScripts);
    }

    [Fact]
    public void Normalize_ShouldSkipUnknownFiles_WithNoPluginEquivalentReason()
    {
        var draft = Normalize("README.md", "agents/a.md");

        draft.Manifest.Skipped.Should().ContainSingle().Which.Should().Be(new SkippedItemResult("README.md", DraftNormalizer.NoPluginEquivalentReason));
    }

    [Fact]
    public void Normalize_ShouldThrowUploadEmpty_WhenNoAssetsRemain()
    {
        var act = () => Normalize("README.md");

        act.Should().Throw<BadRequestCoreException>().Where(x => x.ErrorCode == ErrorCodes.UploadEmpty);
    }

    [Fact]
    public void Normalize_ShouldSetUploadedAtAndStrippedPaths_WhenManifestGenerated()
    {
        var uploadedAt = DateTimeOffset.UtcNow.AddMinutes(-5);

        var draft = DraftNormalizer.Normalize([new UploadedFile("agents/a.md", Encoding.UTF8.GetBytes("agent"))], [".env"], UploadsOptionsFactory.Default(), uploadedAt);

        draft.Manifest.UploadedAt.Should().Be(uploadedAt);
        draft.Manifest.StrippedPaths.Should().Equal(".env");
    }

    private static NormalizedDraft Normalize(params string[] paths)
    {
        List<UploadedFile> files = [.. paths.Select(path => new UploadedFile(path, Encoding.UTF8.GetBytes(path)))];
        return DraftNormalizer.Normalize(files, [], UploadsOptionsFactory.Default(), DateTimeOffset.UtcNow);
    }
}
