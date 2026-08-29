using System.Text;
using System.Text.Json;
using E3A.Application.Publishing.Shared;
using E3A.Tests.Teams.Shared;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Publishing.Shared;

public sealed class TeamTreeAssemblerTests
{
    private readonly Guid _ownerUserId = Guid.NewGuid();

    [Fact]
    public void Assemble_ShouldNamespaceSkillFolders_WhenMembersContributeSkills()
    {
        var members = new List<TeamMemberSnapshot>
        {
            TeamSnapshotFactory.MemberSnapshot("alpha", "skills/house-rules/SKILL.md"),
            TeamSnapshotFactory.MemberSnapshot("beta", "skills/house-rules/SKILL.md"),
        };

        var files = Assemble(members);

        files.Select(x => x.Path).Should().Contain(["skills/alpha--house-rules/SKILL.md", "skills/beta--house-rules/SKILL.md"]);
        files.Select(x => x.Path).Should().NotContain("skills/house-rules/SKILL.md");
    }

    [Fact]
    public void Assemble_ShouldNamespaceSkillsEvenWithoutCollision_WhenOnlyOneMemberHasThem()
    {
        var files = Assemble([TeamSnapshotFactory.MemberSnapshot("alpha", "skills/x/SKILL.md")]);

        files.Select(x => x.Path).Should().Contain("skills/alpha--x/SKILL.md");
    }

    [Fact]
    public void Assemble_ShouldKeepAgentNames_WhenNoCollisionExists()
    {
        var members = new List<TeamMemberSnapshot>
        {
            TeamSnapshotFactory.MemberSnapshot("alpha", "agents/reviewer.md"),
            TeamSnapshotFactory.MemberSnapshot("beta", "agents/builder.md"),
        };

        var files = Assemble(members);

        files.Select(x => x.Path).Should().Contain(["agents/reviewer.md", "agents/builder.md"]);
    }

    [Fact]
    public void Assemble_ShouldPrefixEveryCollidingAgent_WhenTwoMembersShareAFileName()
    {
        var members = new List<TeamMemberSnapshot>
        {
            TeamSnapshotFactory.MemberSnapshot("alpha", "agents/reviewer.md"),
            TeamSnapshotFactory.MemberSnapshot("beta", "agents/reviewer.md"),
        };

        var files = Assemble(members);

        files.Select(x => x.Path).Should().Contain(["agents/alpha--reviewer.md", "agents/beta--reviewer.md"]);
        files.Select(x => x.Path).Should().NotContain("agents/reviewer.md");
    }

    [Fact]
    public void Assemble_ShouldPrefixEveryCollidingCommand_WhenTwoMembersShareAFileName()
    {
        var members = new List<TeamMemberSnapshot>
        {
            TeamSnapshotFactory.MemberSnapshot("alpha", "commands/ship.md"),
            TeamSnapshotFactory.MemberSnapshot("beta", "commands/ship.md"),
        };

        var files = Assemble(members);

        files.Select(x => x.Path).Should().Contain(["commands/alpha--ship.md", "commands/beta--ship.md"]);
        files.Select(x => x.Path).Should().NotContain("commands/ship.md");
    }

    [Fact]
    public void Assemble_ShouldDropNonInstallableRoots_WhenMembersShipHooksOrMcp()
    {
        var files = Assemble([TeamSnapshotFactory.MemberSnapshot("alpha", "agents/reviewer.md", "hooks/hooks.json", ".mcp.json", "output-styles/x.md")]);

        files.Select(x => x.Path).Should().NotContain(["hooks/hooks.json", ".mcp.json", "output-styles/x.md"]);
        files.Select(x => x.Path).Should().Contain("agents/reviewer.md");
    }

    [Fact]
    public void Assemble_ShouldDropFilesMissingFromTheMemberManifest_WhenSnapshotHasExtraFiles()
    {
        var member = TeamSnapshotFactory.MemberSnapshot("alpha", "agents/reviewer.md");
        var withExtra = member with { SnapshotAssets = [.. member.SnapshotAssets, new PluginFile("agents/rogue.md", Encoding.UTF8.GetBytes("rogue"))] };

        var files = Assemble([withExtra]);

        files.Select(x => x.Path).Should().NotContain("agents/rogue.md");
    }

    [Fact]
    public void Assemble_ShouldIncludeTeamPluginJson_WhenCalled()
    {
        var files = Assemble([TeamSnapshotFactory.MemberSnapshot("alpha", "agents/reviewer.md")]);

        var pluginJson = files.Should().ContainSingle(x => x.Path == PluginJsonGenerator.PluginJsonPath).Subject;
        var manifest = JsonSerializer.Deserialize<PluginManifest>(Encoding.UTF8.GetString(pluginJson.Content), JsonSerializerOptions.Web)!;
        manifest.Name.Should().Be($"e3a-team-{TeamFactory.DefaultSlug}");
        manifest.Version.Should().Be("1.0.0");
        manifest.Author.Url.Should().EndWith($"/t/{TeamFactory.DefaultSlug}");
    }

    [Fact]
    public void Assemble_ShouldOrderFilesOrdinallyByPath_WhenCalled()
    {
        var members = new List<TeamMemberSnapshot>
        {
            TeamSnapshotFactory.MemberSnapshot("zeta", "skills/z/SKILL.md", "commands/ship.md"),
            TeamSnapshotFactory.MemberSnapshot("alpha", "agents/reviewer.md"),
        };

        var files = Assemble(members);

        files.Select(x => x.Path).Should().BeInAscendingOrder(StringComparer.Ordinal);
    }

    private List<PluginFile> Assemble(List<TeamMemberSnapshot> members)
        => TeamTreeAssembler.Assemble(members, TeamFactory.Draft(_ownerUserId), "1.0.0", "mmohsen", PublishingOptionsFactory.Default());
}
