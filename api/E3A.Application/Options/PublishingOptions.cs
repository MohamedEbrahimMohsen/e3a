namespace E3A.Application.Options;

public sealed class PublishingOptions
{
    public const string SectionName = "Publishing";

    public int MaxVersionsPerItem { get; set; }
    public int QueueVisibilityTimeoutSeconds { get; set; }
    public string PublicSiteUrl { get; set; } = string.Empty;
    public string MarketplaceName { get; set; } = string.Empty;
    public string MarketplaceOwnerName { get; set; } = string.Empty;
    public string MarketplaceCacheControl { get; set; } = string.Empty;
    public string ZipCacheControl { get; set; } = string.Empty;
    public int MarketplacePageSize { get; set; }
    public int MarketplaceMaxPages { get; set; }
    public int MaxPluginFileCount { get; set; }
    public long MaxPluginBytes { get; set; }
    public int SemanticVersionMaxLength { get; set; }
    public int BlobPathMaxLength { get; set; }
    public int FailureReasonMaxLength { get; set; }
    public int MaxScanFindings { get; set; }
    public int ScanExcerptMaxLength { get; set; }
    public int ScanReportJsonMaxLength { get; set; }
    public long MaxPluginFileBytes { get; set; }
    public int ScanMaxLineLength { get; set; }
    public int ScanOpaqueLineMaxLength { get; set; }
    public int ScanOpaqueLineWrapperMaxLength { get; set; }
}
