using E3a.Core.Options;
using Microsoft.Extensions.Options;

namespace E3a.Core.Tests;

/// <summary>Options factories mirroring the committed appsettings.json defaults.</summary>
public static class TestOptions
{
    public static IOptions<PublishingOptions> Publishing()
    {
        return Microsoft.Extensions.Options.Options.Create(new PublishingOptions
        {
            MaxFilesPerSkill = 40,
            MaxBytesPerSkill = 5 * 1024 * 1024,
            MaxSkillSlugLength = 64,
            AllowedSkillExtensions = [".md", ".json", ".txt", ".yml", ".yaml", ".toml", ".xml", ".csv", ".png", ".jpg", ".jpeg", ".svg"],
            MaxEngineersPerCreator = 50,
            MaxTeamsPerCreator = 10,
            MaxVersionsPerItem = 50,
        });
    }

    public static IOptions<MarketplaceOptions> Marketplace()
    {
        return Microsoft.Extensions.Options.Options.Create(new MarketplaceOptions
        {
            SiteUrl = "https://e3a.dev",
            MarketplaceName = "e3a",
            ZipPathPrefix = "z",
        });
    }
}
