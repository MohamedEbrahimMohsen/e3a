using System.Text;
using Core.Errors;
using E3A.Application.Engineers.UploadEngineerDraft;
using E3A.Application.Exceptions;
using E3A.Tests.Engineers.Shared;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Engineers.UploadEngineerDraft;

public sealed class UploadPathNormalizerTests
{
    [Fact]
    public void Normalize_ShouldUnwrapSingleRootFolder_WhenRootIsNotRecognized()
    {
        var normalized = UploadPathNormalizer.Normalize(Files("upload/skills/a/SKILL.md", "upload/CLAUDE.md"), UploadsOptionsFactory.Default());

        normalized.Select(file => file.Path).Should().Equal("skills/a/SKILL.md", "CLAUDE.md");
    }

    [Fact]
    public void Normalize_ShouldUnwrapNestedRoots_WhenZipWrapsRepoAndClaudeFolder()
    {
        var normalized = UploadPathNormalizer.Normalize(Files("myrepo/.claude/agents/x.md"), UploadsOptionsFactory.Default());

        normalized.Select(file => file.Path).Should().Equal("agents/x.md");
    }

    [Fact]
    public void Normalize_ShouldNotUnwrap_WhenRootIsRecognizedFolder()
    {
        var normalized = UploadPathNormalizer.Normalize(Files("skills/a/SKILL.md", "skills/a/reference.md"), UploadsOptionsFactory.Default());

        normalized.Select(file => file.Path).Should().Equal("skills/a/SKILL.md", "skills/a/reference.md");
    }

    [Fact]
    public void Normalize_ShouldStripClaudePrefix_WhenClaudeFolderSitsBesideClaudeMd()
    {
        var normalized = UploadPathNormalizer.Normalize(Files("CLAUDE.md", ".claude/skills/a/SKILL.md"), UploadsOptionsFactory.Default());

        normalized.Select(file => file.Path).Should().Equal("CLAUDE.md", "skills/a/SKILL.md");
    }

    [Fact]
    public void Normalize_ShouldThrowDuplicatePath_WhenTwoFilesCollide()
    {
        var act = () => UploadPathNormalizer.Normalize(Files("CLAUDE.md", ".claude/skills/a/SKILL.md", "skills/a/skill.md"), UploadsOptionsFactory.Default());

        act.Should().Throw<BadRequestCoreException>().Where(x => x.ErrorCode == ErrorCodes.UploadDuplicatePath);
    }

    [Theory]
    [InlineData("tool.exe")]
    [InlineData("bin/mytool")]
    public void Normalize_ShouldThrowFileTypeNotAllowed_WhenExtensionIsNotAllowed(string path)
    {
        var act = () => UploadPathNormalizer.Normalize(Files("CLAUDE.md", path), UploadsOptionsFactory.Default());

        act.Should().Throw<BadRequestCoreException>().Where(x => x.ErrorCode == ErrorCodes.UploadFileTypeNotAllowed);
    }

    private static List<UploadedFile> Files(params string[] paths)
    {
        return [.. paths.Select(path => new UploadedFile(path, Encoding.UTF8.GetBytes(path)))];
    }
}
