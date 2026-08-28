using E3A.Application.Options;

namespace E3A.Tests.Publishing.Shared;

public static class PublishingOptionsFactory
{
    public const string PublicSiteUrl = "https://e3a.dev";
    public const string MarketplaceCacheControl = "public, max-age=60";
    public const string ZipCacheControl = "public, max-age=31536000, immutable";

    public static PublishingOptions Default(int maxVersionsPerItem = 50, int marketplacePageSize = 100, int marketplaceMaxPages = 50, int maxPluginFileCount = 400, long maxPluginBytes = 104857600, int queueVisibilityTimeoutSeconds = 10)
    {
        return new PublishingOptions
        {
            MaxVersionsPerItem = maxVersionsPerItem,
            QueueVisibilityTimeoutSeconds = queueVisibilityTimeoutSeconds,
            PublicSiteUrl = PublicSiteUrl,
            MarketplaceName = "e3a",
            MarketplaceOwnerName = "e3a",
            MarketplaceCacheControl = MarketplaceCacheControl,
            ZipCacheControl = ZipCacheControl,
            MarketplacePageSize = marketplacePageSize,
            MarketplaceMaxPages = marketplaceMaxPages,
            MaxPluginFileCount = maxPluginFileCount,
            MaxPluginBytes = maxPluginBytes,
            SemanticVersionMaxLength = 20,
            BlobPathMaxLength = 400,
            FailureReasonMaxLength = 500,
        };
    }
}
