namespace E3A.Application.Publishing.Shared;

public static class PluginName
{
    // The installed plugin identity inside Claude Code. Changing it breaks every existing install.
    private const string Prefix = "e3a-";

    public static string For(string slug)
    {
        return $"{Prefix}{slug}";
    }
}
