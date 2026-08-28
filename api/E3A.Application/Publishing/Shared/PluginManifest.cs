namespace E3A.Application.Publishing.Shared;

public sealed record PluginManifest(string Name, string Version, string? Description, PluginAuthor Author);

public sealed record PluginAuthor(string Name, string Url);
