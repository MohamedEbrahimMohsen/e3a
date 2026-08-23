namespace E3a.Core.Domain;

/// <summary>Frozen composition of an engineer at publish time (stored as FrozenManifestJson).</summary>
public sealed record EngineerManifest(
    string Slug,
    string DisplayName,
    string Description,
    string OwnerLogin,
    string OwnerUrl,
    string? PersonaMarkdown,
    IReadOnlyList<string> Tags,
    IReadOnlyList<SkillFolder> Skills)
{
    public string PluginName => $"{E3aConventions.PluginNamePrefix}-{OwnerLogin.ToLowerInvariant()}-{Slug}";
}

/// <summary>A normalized skill: SKILL.md at the root plus subsidiary files.</summary>
public sealed record SkillFolder(string Slug, IReadOnlyList<PluginFile> Files);

/// <summary>Frozen composition of a team: members are engineer manifests at their pinned versions.</summary>
public sealed record TeamManifest(
    string Slug,
    string DisplayName,
    string Description,
    string OwnerLogin,
    string OwnerUrl,
    IReadOnlyList<string> Tags,
    IReadOnlyList<TeamMember> Members)
{
    public string PluginName => $"{E3aConventions.PluginNamePrefix}-{OwnerLogin.ToLowerInvariant()}-{Slug}";
}

public sealed record TeamMember(EngineerManifest Engineer, string PinnedSemanticVersion);
