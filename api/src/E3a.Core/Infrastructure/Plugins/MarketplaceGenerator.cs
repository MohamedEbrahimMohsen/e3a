using System.Text.Json;
using E3a.Core.Options;
using Microsoft.Extensions.Options;

namespace E3a.Core.Infrastructure.Plugins;

public sealed record MarketplacePlugin(string Name, string Description, string Version, string AuthorLogin, string AuthorUrl, IReadOnlyList<string> Keywords, string ZipUrl, string Sha256);

/// <summary>Generates the marketplace.json Claude Code consumes via /plugin marketplace add.</summary>
public sealed class MarketplaceGenerator(IOptions<MarketplaceOptions> marketplaceOptions)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly MarketplaceOptions options = marketplaceOptions.Value;

    public string Generate(IEnumerable<MarketplacePlugin> plugins)
    {
        var document = new
        {
            name = options.MarketplaceName,
            owner = new { name = options.MarketplaceName, url = options.SiteUrl },
            plugins = plugins
                .OrderBy(p => p.Name, StringComparer.Ordinal)
                .Select(p => new
                {
                    name = p.Name,
                    description = p.Description,
                    version = p.Version,
                    author = new { name = $"@{p.AuthorLogin}", url = p.AuthorUrl },
                    keywords = p.Keywords,
                    source = new { source = "archive", url = p.ZipUrl, sha256 = p.Sha256 },
                })
                .ToArray(),
        };
        return JsonSerializer.Serialize(document, JsonOptions);
    }

    public string GetZipUrl(string pluginName, string semanticVersion)
    {
        return $"{options.SiteUrl.TrimEnd('/')}/{options.ZipPathPrefix}/{pluginName}/{semanticVersion}.zip";
    }
}
