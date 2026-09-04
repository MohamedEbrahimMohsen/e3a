using System.Text;
using E3A.Application.Options;
using E3A.Domain.Engineers;
using E3A.Domain.Teams;

namespace E3A.Application.Publishing.Shared;

public static class PluginJsonGenerator
{
    // The exact path Claude Code's plugin loader resolves; renaming it makes the plugin invisible.
    public const string PluginJsonPath = ".claude-plugin/plugin.json";

    public static PluginFile Generate(Engineer engineer, string semanticVersion, string authorName, PublishingOptions options)
    {
        var author = new PluginAuthor(authorName, PublicCatalogUrl.ForEngineer(options.PublicSiteUrl, engineer.Slug));
        var manifest = new PluginManifest(PluginName.ForEngineer(engineer.Slug), semanticVersion, engineer.Description, author);

        return new PluginFile(PluginJsonPath, Encoding.UTF8.GetBytes(PluginJsonSerializer.Serialize(manifest)));
    }

    public static PluginFile Generate(Team team, string semanticVersion, string authorName, PublishingOptions options)
    {
        var author = new PluginAuthor(authorName, PublicCatalogUrl.ForTeam(options.PublicSiteUrl, team.Slug));
        var manifest = new PluginManifest(PluginName.ForTeam(team.Slug), semanticVersion, team.Description, author);

        return new PluginFile(PluginJsonPath, Encoding.UTF8.GetBytes(PluginJsonSerializer.Serialize(manifest)));
    }
}
