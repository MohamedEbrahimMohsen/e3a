namespace E3A.Application.Publishing.Shared;

public static class PluginName
{
    // The installed plugin identity inside Claude Code. Changing it breaks every existing install.
    private const string Prefix = "e3a-";

    // Teams carry their own namespace segment. Engineer slugs are barred from starting with it
    // (IsTeamNamespaced, enforced by the engineer slug validators) — that one-directional guard is
    // what makes the two plugin namespaces disjoint, not the segment on its own.
    private const string TeamSegment = "team-";

    public static string ForEngineer(string slug)
    {
        return $"{Prefix}{slug}";
    }

    public static string ForTeam(string slug)
    {
        return $"{Prefix}{TeamSegment}{slug}";
    }

    public static bool IsTeamNamespaced(string slug)
    {
        return slug.StartsWith(TeamSegment, StringComparison.OrdinalIgnoreCase);
    }
}
