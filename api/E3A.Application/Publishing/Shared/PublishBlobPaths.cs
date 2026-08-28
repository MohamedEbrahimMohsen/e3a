namespace E3A.Application.Publishing.Shared;

public static class PublishBlobPaths
{
    // Content types are wire protocol, not tunables — a browser and Claude Code both key off them.
    public const string ZipContentType = "application/zip";
    public const string MarketplaceContentType = "application/json";

    // The exact blob name `/plugin marketplace add https://<domain>/marketplace.json` resolves.
    public const string RootMarketplaceBlobName = "marketplace.json";

    public static string DraftPrefix(Guid ownerUserId, Guid engineerId)
    {
        return $"{ownerUserId}/{engineerId}/";
    }

    public static string SnapshotPrefix(Guid versionId)
    {
        return $"{versionId}/";
    }

    public static string Zip(string pluginName, string semanticVersion)
    {
        return $"z/{pluginName}/{semanticVersion}.zip";
    }

    public static string PinnedMarketplace(string pluginName, string semanticVersion)
    {
        return $"m/{pluginName}/{semanticVersion}/marketplace.json";
    }

    public static string ZipUrl(string publicSiteUrl, string zipBlobPath)
    {
        return $"{publicSiteUrl.TrimEnd('/')}/{zipBlobPath}";
    }
}
