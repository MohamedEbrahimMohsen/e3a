using System.Text;
using E3a.Core.Domain;
using E3a.Core.Infrastructure.Validation;

namespace E3a.Core.Tests;

public class StructureValidatorTests
{
    private static readonly StructureValidator Validator = new(TestOptions.Publishing());

    private static PluginFile File(string path, string content = "x")
    {
        return new PluginFile(path, Encoding.UTF8.GetBytes(content));
    }

    private static PluginFile SkillMd(string slug)
    {
        return File($"skills/{slug}/SKILL.md", $"---\nname: {slug}\ndescription: d\n---\nbody");
    }

    private static PluginPackage Package(params PluginFile[] files)
    {
        return new PluginPackage("e3a-mo-x", "1.0.0", [File(".claude-plugin/plugin.json", "{}"), .. files]);
    }

    [Fact]
    public void Valid_package_has_no_errors()
    {
        Assert.Empty(Validator.Validate(Package(SkillMd("good-skill"))));
    }

    [Fact]
    public void Missing_plugin_json_is_an_error()
    {
        var package = new PluginPackage("e3a-mo-x", "1.0.0", [SkillMd("s")]);
        Assert.Contains(Validator.Validate(package), e => e.Contains("plugin.json"));
    }

    [Fact]
    public void Missing_skill_md_is_an_error()
    {
        Assert.Contains(Validator.Validate(Package(File("skills/s/notes.md"))), e => e.Contains("missing SKILL.md"));
    }

    [Fact]
    public void Path_traversal_is_an_error()
    {
        Assert.Contains(Validator.Validate(Package(SkillMd("s"), File("skills/s/../../evil.md"))), e => e.Contains("Unsafe path"));
    }

    [Fact]
    public void Disallowed_extension_is_an_error()
    {
        Assert.Contains(Validator.Validate(Package(SkillMd("s"), File("skills/s/run.exe"))), e => e.Contains("Disallowed file type"));
    }

    [Fact]
    public void Uppercase_slug_is_an_error()
    {
        Assert.Contains(Validator.Validate(Package(File("skills/BadSlug/SKILL.md", "---\nname: x\ndescription: d\n---"))), e => e.Contains("Invalid skill slug"));
    }

    [Fact]
    public void Missing_frontmatter_description_is_an_error()
    {
        Assert.Contains(Validator.Validate(Package(File("skills/s/SKILL.md", "---\nname: s\n---\nbody"))), e => e.Contains("description"));
    }

    [Fact]
    public void Oversize_skill_is_an_error()
    {
        var big = new PluginFile("skills/s/big.md", new byte[TestOptions.Publishing().Value.MaxBytesPerSkill + 1]);
        Assert.Contains(Validator.Validate(Package(SkillMd("s"), big)), e => e.Contains("max"));
    }

    [Fact]
    public void Too_many_files_is_an_error()
    {
        var files = Enumerable.Range(0, TestOptions.Publishing().Value.MaxFilesPerSkill + 1).Select(i => File($"skills/s/f{i}.md")).ToArray();
        Assert.Contains(Validator.Validate(Package([SkillMd("s"), .. files])), e => e.Contains("files (max"));
    }
}
