using System.Text;
using System.Text.Json;
using E3a.Core.Domain;
using E3a.Core.Infrastructure.Plugins;

namespace E3a.Core.Tests;

public class PackageComposerTests
{
    private static SkillFolder Skill(string slug)
    {
        return new SkillFolder(slug, [new PluginFile("SKILL.md", Encoding.UTF8.GetBytes($"---\nname: {slug}\ndescription: d\n---\nbody"))]);
    }

    private static EngineerManifest Engineer(string slug = "dive-backend-engineer")
    {
        return new EngineerManifest(slug, "Dive Backend Engineer", "A .NET DDD specialist.", "mohamed", "https://github.com/mohamed", null, ["backend"], [Skill("ddd-slices"), Skill("ef-tuning")]);
    }

    [Fact]
    public void ComposeEngineer_produces_expected_tree()
    {
        var package = new PackageComposer().ComposeEngineer(Engineer(), "2.0.0");

        Assert.Equal("e3a-mohamed-dive-backend-engineer", package.PluginName);
        Assert.NotNull(package.Find(".claude-plugin/plugin.json"));
        Assert.NotNull(package.Find("agents/dive-backend-engineer.md"));
        Assert.NotNull(package.Find("commands/dive-backend-engineer.md"));
        Assert.NotNull(package.Find("skills/ddd-slices/SKILL.md"));
        Assert.NotNull(package.Find("skills/ef-tuning/SKILL.md"));
    }

    [Fact]
    public void ComposeEngineer_plugin_json_carries_attribution_and_version()
    {
        var package = new PackageComposer().ComposeEngineer(Engineer(), "2.0.0");
        using var document = JsonDocument.Parse(package.Find(".claude-plugin/plugin.json")!.AsText());

        Assert.Equal("2.0.0", document.RootElement.GetProperty("version").GetString());
        Assert.Equal("@mohamed", document.RootElement.GetProperty("author").GetProperty("name").GetString());
    }

    [Fact]
    public void ComposeEngineer_generates_default_persona_when_absent()
    {
        var package = new PackageComposer().ComposeEngineer(Engineer(), "1.0.0");
        var persona = package.Find("agents/dive-backend-engineer.md")!.AsText();

        Assert.Contains("Dive Backend Engineer", persona);
        Assert.Contains("ddd-slices", persona);
    }

    [Fact]
    public void ComposeTeam_namespaces_member_skills_with_double_hyphen()
    {
        var team = new TeamManifest("dotnet-squad", ".NET Squad", "Full team.",
            "mohamed", "https://github.com/mohamed", [],
            [new TeamMember(Engineer(), "2.0.0"), new TeamMember(Engineer("qa-engineer"), "1.0.0")]);

        var package = new PackageComposer().ComposeTeam(team, "1.0.0");

        Assert.Equal("e3a-mohamed-dotnet-squad", package.PluginName);
        Assert.NotNull(package.Find("agents/dive-backend-engineer.md"));
        Assert.NotNull(package.Find("agents/qa-engineer.md"));
        Assert.NotNull(package.Find("skills/dive-backend-engineer--ddd-slices/SKILL.md"));
        Assert.NotNull(package.Find("skills/qa-engineer--ef-tuning/SKILL.md"));
        Assert.NotNull(package.Find("commands/dotnet-squad.md"));
    }
}
