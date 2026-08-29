namespace E3A.Application.Publishing.Shared;

public static class PluginName
{
    // The installed plugin identity inside Claude Code. Changing it breaks every existing install.
    private const string Prefix = "e3a-";

    // Teams carry their own namespace segment so a team slug can never collide with an engineer slug.
    private const string TeamSegment = "team-";

    public static string ForEngineer(string slug)
    {
        return $"{Prefix}{slug}";
    }

    public static string ForTeam(string slug)
    {
        return $"{Prefix}{TeamSegment}{slug}";
    }
}
