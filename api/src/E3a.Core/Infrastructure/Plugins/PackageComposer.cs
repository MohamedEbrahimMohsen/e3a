using System.Text;
using System.Text.Json;
using E3a.Core.Domain;

namespace E3a.Core.Infrastructure.Plugins;

/// <summary>Turns frozen manifests into the concrete plugin file tree (PluginPackage).</summary>
public sealed class PackageComposer
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public PluginPackage ComposeEngineer(EngineerManifest engineer, string semanticVersion)
    {
        var files = new List<PluginFile>
        {
            PluginJson(engineer.PluginName, semanticVersion, engineer.Description, engineer.OwnerLogin, engineer.OwnerUrl, engineer.Tags),
            new($"agents/{engineer.Slug}.md", Utf8(engineer.PersonaMarkdown ?? DefaultPersona(engineer))),
            new($"commands/{engineer.Slug}.md", Utf8(DispatchCommand(engineer.Slug, engineer.DisplayName))),
        };

        foreach (var skill in engineer.Skills)
        {
            files.AddRange(skill.Files.Select(f => f with { RelativePath = $"skills/{skill.Slug}/{f.RelativePath}" }));
        }

        return new PluginPackage(engineer.PluginName, semanticVersion, files);
    }

    public PluginPackage ComposeTeam(TeamManifest team, string semanticVersion)
    {
        var files = new List<PluginFile>
        {
            PluginJson(team.PluginName, semanticVersion, team.Description, team.OwnerLogin, team.OwnerUrl, team.Tags),
            new($"commands/{team.Slug}.md", Utf8(TeamCommand(team))),
        };

        foreach (var member in team.Members)
        {
            var engineer = member.Engineer;
            files.Add(new PluginFile($"agents/{engineer.Slug}.md", Utf8(engineer.PersonaMarkdown ?? DefaultPersona(engineer))));
            foreach (var skill in engineer.Skills)
            {
                files.AddRange(skill.Files.Select(f => f with { RelativePath = $"skills/{engineer.Slug}--{skill.Slug}/{f.RelativePath}" }));
            }
        }

        return new PluginPackage(team.PluginName, semanticVersion, files);
    }

    private static PluginFile PluginJson(string name, string semanticVersion, string description, string ownerLogin, string ownerUrl, IReadOnlyList<string> tags)
    {
        var json = JsonSerializer.Serialize(new
        {
            name,
            version = semanticVersion,
            description,
            author = new { name = $"@{ownerLogin}", url = ownerUrl },
            keywords = tags,
        }, JsonOptions);
        return new PluginFile(".claude-plugin/plugin.json", Utf8(json));
    }

    private static string DefaultPersona(EngineerManifest engineer)
    {
        var persona = $"""
            ---
            name: {engineer.Slug}
            description: {engineer.Description}
            ---

            You are **{engineer.DisplayName}**, a specialized engineer agent.

            {engineer.Description}

            Your installed skills:
            {string.Join("\n", engineer.Skills.Select(s => $"- {s.Slug}"))}

            Apply these skills when relevant, follow the conventions they define, and stay
            within your specialty — flag work outside it rather than guessing.
            """;
        return persona;
    }

    private static string DispatchCommand(string slug, string displayName)
    {
        var command = $"""
            ---
            description: Engage {displayName}
            ---

            Delegate the following task to the `{slug}` agent and relay its result: $ARGUMENTS
            """;
        return command;
    }

    private static string TeamCommand(TeamManifest team)
    {
        var command = $"""
            ---
            description: {team.DisplayName} — team overview and dispatch
            ---

            You lead the **{team.DisplayName}** team. Members:
            {string.Join("\n", team.Members.Select(m => $"- `{m.Engineer.Slug}` — {m.Engineer.Description}"))}

            For the following request, decide which member agent(s) to delegate to, dispatch
            the work, and consolidate the results: $ARGUMENTS
            """;
        return command;
    }

    private static byte[] Utf8(string value)
    {
        return Encoding.UTF8.GetBytes(value);
    }
}
