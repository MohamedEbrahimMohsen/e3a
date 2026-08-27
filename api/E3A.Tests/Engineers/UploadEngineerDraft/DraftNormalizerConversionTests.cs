using System.Text;
using E3A.Application.Engineers.Shared;
using E3A.Application.Engineers.UploadEngineerDraft;
using E3A.Tests.Engineers.Shared;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Engineers.UploadEngineerDraft;

public sealed class DraftNormalizerConversionTests
{
    private const string HooksSettings = """{"hooks":{"PreToolUse":[{"matcher":"Bash","hooks":[{"type":"command","command":"echo hi"}]}]}}""";

    [Fact]
    public void Normalize_ShouldGenerateHouseRulesSkill_WhenClaudeMdAndRuleFoldersPresent()
    {
        var draft = Normalize(("CLAUDE.md", "root rules"), ("rules/x.md", "extra rules"));

        draft.Assets.Select(asset => asset.Path).Should().Equal("skills/house-rules/SKILL.md");
        draft.Manifest.Converted.Select(item => item.SourcePath).Should().Equal("CLAUDE.md", "rules/x.md");
        draft.Manifest.Converted.Should().OnlyContain(item => item.TargetPath == "skills/house-rules/SKILL.md" && item.Reason == HouseRulesSkillGenerator.MergedIntoHouseRulesReason);
        draft.Manifest.ClaudeMdSnippet.Should().Be(HouseRulesSkillGenerator.ClaudeMdSnippet);
    }

    [Fact]
    public void Normalize_ShouldPrefixGeneratedSkill_WhenUploadAlreadyContainsHouseRules()
    {
        var draft = Normalize(("CLAUDE.md", "root rules"), ("skills/house-rules/SKILL.md", "creator skill"));

        draft.Assets.Select(asset => asset.Path).Should().Equal("skills/house-rules/SKILL.md", "skills/e3a-house-rules/SKILL.md");
        draft.Manifest.Converted.Should().ContainSingle().Which.TargetPath.Should().Be("skills/e3a-house-rules/SKILL.md");
    }

    [Fact]
    public void Normalize_ShouldSkipNonTextFilesUnderRuleFolders_WhenPresent()
    {
        var draft = Normalize(("CLAUDE.md", "root rules"), ("docs/diagram.png", "binary"));

        draft.Manifest.Skipped.Should().ContainSingle().Which.Should().Be(new SkippedItemResult("docs/diagram.png", DraftNormalizer.NotConvertibleReason));
    }

    [Fact]
    public void Normalize_ShouldPreferUploadedHooksJson_WhenBothSourcesPresent()
    {
        var draft = Normalize(("hooks/hooks.json", "{}"), ("settings.json", HooksSettings));

        draft.Assets.Select(asset => asset.Path).Should().Equal("hooks/hooks.json");
        draft.Manifest.Imported.Should().ContainSingle().Which.Should().Be(new ImportedItemResult("hooks/hooks.json", "hooks/hooks.json", ImportCategories.Hooks));
        draft.Manifest.Skipped.Should().ContainSingle().Which.Should().Be(new SkippedItemResult("settings.json#hooks", SettingsJsonImporter.HooksAlreadyUploadedReason));
    }

    [Fact]
    public void Normalize_ShouldReturnNullSnippetAndNoConversion_WhenNoHouseRuleSources()
    {
        var draft = Normalize(("agents/a.md", "agent"));

        draft.Manifest.ClaudeMdSnippet.Should().BeNull();
        draft.Manifest.Converted.Should().BeEmpty();
    }

    private static NormalizedDraft Normalize(params (string Path, string Content)[] files)
    {
        List<UploadedFile> uploadedFiles = [.. files.Select(file => new UploadedFile(file.Path, Encoding.UTF8.GetBytes(file.Content)))];
        return DraftNormalizer.Normalize(uploadedFiles, [], UploadsOptionsFactory.Default(), DateTimeOffset.UtcNow);
    }
}
