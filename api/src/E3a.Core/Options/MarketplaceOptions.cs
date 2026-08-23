namespace E3a.Core.Options;

public sealed class MarketplaceOptions
{
    public const string SectionName = "Marketplace";

    public string SiteUrl { get; set; } = string.Empty;
    public string MarketplaceName { get; set; } = string.Empty;
    public string ZipPathPrefix { get; set; } = string.Empty;
}
