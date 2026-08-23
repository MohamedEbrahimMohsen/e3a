using System.Text;
using E3a.Core.Domain;
using E3a.Core.Infrastructure.Plugins;

namespace E3a.Core.Tests;

public class PluginBuilderTests
{
    private static PluginPackage SamplePackage()
    {
        return new PluginPackage("e3a-mo-backend", "1.0.0",
        [
            new PluginFile(".claude-plugin/plugin.json", Encoding.UTF8.GetBytes("{\"name\":\"e3a-mo-backend\"}")),
            new PluginFile("skills/ddd/SKILL.md", Encoding.UTF8.GetBytes("---\nname: ddd\ndescription: x\n---\nbody")),
        ]);
    }

    [Fact]
    public void Build_is_deterministic_same_input_same_sha256()
    {
        var builder = new PluginBuilder();
        var first = builder.Build(SamplePackage());
        var second = builder.Build(SamplePackage());

        Assert.Equal(first.Sha256, second.Sha256);
        Assert.Equal(first.ZipBytes, second.ZipBytes);
    }

    [Fact]
    public void Build_different_content_different_sha256()
    {
        var builder = new PluginBuilder();
        var original = builder.Build(SamplePackage());
        var changed = builder.Build(new PluginPackage("e3a-mo-backend", "1.0.0",
            [new PluginFile("skills/ddd/SKILL.md", Encoding.UTF8.GetBytes("different"))]));

        Assert.NotEqual(original.Sha256, changed.Sha256);
    }

    [Fact]
    public void Build_file_order_does_not_change_sha256()
    {
        var builder = new PluginBuilder();
        var files = SamplePackage().Files;
        var reversed = new PluginPackage("e3a-mo-backend", "1.0.0", files.Reverse().ToList());

        Assert.Equal(builder.Build(SamplePackage()).Sha256, builder.Build(reversed).Sha256);
    }

    [Fact]
    public void BlobPath_is_versioned_and_immutable_shaped()
    {
        var built = new PluginBuilder().Build(SamplePackage());
        Assert.Equal("z/e3a-mo-backend/1.0.0.zip", built.GetBlobPath(TestOptions.Marketplace().Value.ZipPathPrefix));
    }
}
