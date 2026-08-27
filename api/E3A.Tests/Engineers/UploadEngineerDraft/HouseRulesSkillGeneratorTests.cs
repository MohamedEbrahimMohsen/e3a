using System.Text;
using E3A.Application.Engineers.UploadEngineerDraft;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Engineers.UploadEngineerDraft;

public sealed class HouseRulesSkillGeneratorTests
{
    [Fact]
    public void Generate_ShouldEmitFrontMatterAndAllSources_WhenSourcesProvided()
    {
        var generation = HouseRulesSkillGenerator.Generate(Sources(("CLAUDE.md", "root rules"), ("rules/x.md", "extra rules")), "house-rules");

        var content = Encoding.UTF8.GetString(generation.SkillFile.Content);
        content.Should().Contain("name: house-rules");
        content.Should().Contain("description: House rules imported");
        content.Should().Contain("## Source: CLAUDE.md").And.Contain("root rules");
        content.Should().Contain("## Source: rules/x.md").And.Contain("extra rules");
        generation.SkillFile.Path.Should().Be("skills/house-rules/SKILL.md");
        generation.ClaudeMdSnippet.Should().Be(HouseRulesSkillGenerator.ClaudeMdSnippet);
    }

    [Fact]
    public void Generate_ShouldTargetGivenFolder_WhenFolderNameProvided()
    {
        var generation = HouseRulesSkillGenerator.Generate(Sources(("CLAUDE.md", "root rules")), "e3a-house-rules");

        generation.SkillFile.Path.Should().Be("skills/e3a-house-rules/SKILL.md");
        Encoding.UTF8.GetString(generation.SkillFile.Content).Should().Contain("name: e3a-house-rules");
        generation.Converted.Should().ContainSingle().Which.TargetPath.Should().Be("skills/e3a-house-rules/SKILL.md");
    }

    private static List<UploadedFile> Sources(params (string Path, string Content)[] sources)
    {
        return [.. sources.Select(source => new UploadedFile(source.Path, Encoding.UTF8.GetBytes(source.Content)))];
    }
}
