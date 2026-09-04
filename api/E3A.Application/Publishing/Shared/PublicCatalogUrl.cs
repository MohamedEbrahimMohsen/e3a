namespace E3A.Application.Publishing.Shared;

public static class PublicCatalogUrl
{
    // The public catalog page a plugin's author field points at; the segments match the SPA routes.
    private const string EngineerSegment = "e";
    private const string TeamSegment = "t";

    public static string ForEngineer(string publicSiteUrl, string slug)
    {
        return $"{publicSiteUrl.TrimEnd('/')}/{EngineerSegment}/{slug}";
    }

    public static string ForTeam(string publicSiteUrl, string slug)
    {
        return $"{publicSiteUrl.TrimEnd('/')}/{TeamSegment}/{slug}";
    }
}
