namespace E3A.Application.Publishing.Shared;

public sealed record MarketplaceDocument(string Name, MarketplaceOwner Owner, List<MarketplacePlugin> Plugins);

public sealed record MarketplaceOwner(string Name, string Url);

public sealed record MarketplacePlugin(string Name, string? Description, string Version, PluginAuthor Author, List<string> Keywords, MarketplaceSource Source);

public sealed record MarketplaceSource(string Source, string Url, string Sha256);
