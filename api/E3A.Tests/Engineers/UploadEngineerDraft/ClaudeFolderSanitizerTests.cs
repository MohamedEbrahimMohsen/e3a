using System.Text;
using E3A.Application.Engineers.UploadEngineerDraft;
using E3A.Tests.Engineers.Shared;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Engineers.UploadEngineerDraft;

public sealed class ClaudeFolderSanitizerTests
{
    [Fact]
    public void Sanitize_ShouldStripSettingsLocalJson_WhenPresent()
    {
        var outcome = ClaudeFolderSanitizer.Sanitize(Files("settings.local.json", "settings.json"), UploadsOptionsFactory.Default());

        outcome.Files.Select(file => file.Path).Should().Equal("settings.json");
        outcome.StrippedPaths.Should().Equal("settings.local.json");
    }

    [Fact]
    public void Sanitize_ShouldStripEnvFiles_WhenNameStartsWithEnvPrefix()
    {
        var outcome = ClaudeFolderSanitizer.Sanitize(Files(".env", ".env.local", "CLAUDE.md"), UploadsOptionsFactory.Default());

        outcome.Files.Select(file => file.Path).Should().Equal("CLAUDE.md");
        outcome.StrippedPaths.Should().Equal(".env", ".env.local");
    }

    [Fact]
    public void Sanitize_ShouldStripFilesInsideStrippedFolders_WhenAnySegmentMatches()
    {
        var outcome = ClaudeFolderSanitizer.Sanitize(Files("memory/x.md", "skills/a/sessions/y.md", "skills/a/SKILL.md"), UploadsOptionsFactory.Default());

        outcome.Files.Select(file => file.Path).Should().Equal("skills/a/SKILL.md");
        outcome.StrippedPaths.Should().Equal("memory/x.md", "skills/a/sessions/y.md");
    }

    [Fact]
    public void Sanitize_ShouldStripOsJunk_WhenPresent()
    {
        var outcome = ClaudeFolderSanitizer.Sanitize(Files(".DS_Store", "skills/a/Thumbs.db", "skills/a/SKILL.md"), UploadsOptionsFactory.Default());

        outcome.Files.Select(file => file.Path).Should().Equal("skills/a/SKILL.md");
        outcome.StrippedPaths.Should().Equal(".DS_Store", "skills/a/Thumbs.db");
    }

    [Fact]
    public void Sanitize_ShouldMatchCaseInsensitively_WhenNamesDifferByCase()
    {
        var outcome = ClaudeFolderSanitizer.Sanitize(Files("SETTINGS.LOCAL.JSON"), UploadsOptionsFactory.Default());

        outcome.Files.Should().BeEmpty();
        outcome.StrippedPaths.Should().Equal("SETTINGS.LOCAL.JSON");
    }

    [Fact]
    public void Sanitize_ShouldKeepAllFilesAndRecordNothing_WhenNothingMatches()
    {
        var outcome = ClaudeFolderSanitizer.Sanitize(Files("CLAUDE.md", "skills/a/SKILL.md"), UploadsOptionsFactory.Default());

        outcome.Files.Select(file => file.Path).Should().Equal("CLAUDE.md", "skills/a/SKILL.md");
        outcome.StrippedPaths.Should().BeEmpty();
    }

    private static List<UploadedFile> Files(params string[] paths)
    {
        return [.. paths.Select(path => new UploadedFile(path, Encoding.UTF8.GetBytes(path)))];
    }
}
