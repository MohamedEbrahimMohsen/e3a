using E3A.Application.Options;
using E3A.Domain.Engineers;
using E3A.Domain.Publishing;

namespace E3A.Application.Publishing.Shared;

public static class MarketplaceDocumentGenerator
{
    // Relative source paths do not resolve for a marketplace added by URL; the archive type points at an absolute zip.
    private const string ArchiveSourceType = "archive";

    public static MarketplacePlugin GeneratePlugin(Engineer engineer, ItemVersion version, string authorName, PublishingOptions options)
    {
        var author = new PluginAuthor(authorName, $"{options.PublicSiteUrl.TrimEnd('/')}/e/{engineer.Slug}");
        var source = new MarketplaceSource(ArchiveSourceType, PublishBlobPaths.ZipUrl(options.PublicSiteUrl, version.ZipBlobPath!), version.ZipSha256!);

        return new MarketplacePlugin(PluginName.For(engineer.Slug), engineer.Description, version.SemanticVersion, author, [.. engineer.Tags], source);
    }

    public static string Generate(List<MarketplacePlugin> plugins, PublishingOptions options)
    {
        return PluginJsonSerializer.Serialize(new MarketplaceDocument(options.MarketplaceName, new MarketplaceOwner(options.MarketplaceOwnerName, options.PublicSiteUrl), plugins));
    }
}
