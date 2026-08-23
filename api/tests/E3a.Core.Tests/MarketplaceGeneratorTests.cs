using System.Text.Json;
using E3a.Core.Infrastructure.Plugins;

namespace E3a.Core.Tests;

public class MarketplaceGeneratorTests
{
    private static readonly MarketplaceGenerator Generator = new(TestOptions.Marketplace());

    private static MarketplacePlugin Plugin(string name = "e3a-mohamed-dive-backend-engineer")
    {
        return new MarketplacePlugin(name, "A .NET DDD specialist.", "3.0.0", "mohamed", "https://github.com/mohamed", ["backend", "dotnet"], Generator.GetZipUrl(name, "3.0.0"), new string('a', 64));
    }

    [Fact]
    public void Generates_archive_sources_with_sha256()
    {
        var json = Generator.Generate([Plugin()]);
        using var document = JsonDocument.Parse(json);

        var entry = document.RootElement.GetProperty("plugins")[0];
        var source = entry.GetProperty("source");
        Assert.Equal("archive", source.GetProperty("source").GetString());
        Assert.StartsWith("https://e3a.dev/z/", source.GetProperty("url").GetString());
        Assert.Equal(64, source.GetProperty("sha256").GetString()!.Length);
        Assert.Equal("3.0.0", entry.GetProperty("version").GetString());
        Assert.Equal("@mohamed", entry.GetProperty("author").GetProperty("name").GetString());
    }

    [Fact]
    public void Marketplace_has_name_and_owner()
    {
        var json = Generator.Generate([]);
        using var document = JsonDocument.Parse(json);

        Assert.Equal("e3a", document.RootElement.GetProperty("name").GetString());
        Assert.Equal("https://e3a.dev", document.RootElement.GetProperty("owner").GetProperty("url").GetString());
    }

    [Fact]
    public void Plugins_are_sorted_by_name_for_stable_diffs()
    {
        var json = Generator.Generate([Plugin("e3a-z-last"), Plugin("e3a-a-first")]);
        using var document = JsonDocument.Parse(json);

        var plugins = document.RootElement.GetProperty("plugins");
        Assert.Equal("e3a-a-first", plugins[0].GetProperty("name").GetString());
        Assert.Equal("e3a-z-last", plugins[1].GetProperty("name").GetString());
    }

    [Fact]
    public void Zip_url_is_built_from_marketplace_options()
    {
        Assert.Equal("https://e3a.dev/z/e3a-mo-x/1.0.0.zip", Generator.GetZipUrl("e3a-mo-x", "1.0.0"));
    }
}
