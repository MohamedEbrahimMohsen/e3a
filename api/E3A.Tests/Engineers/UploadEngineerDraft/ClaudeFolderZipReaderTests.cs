using System.Text;
using Core.Errors;
using E3A.Application.Engineers.UploadEngineerDraft;
using E3A.Application.Exceptions;
using E3A.Tests.Engineers.Shared;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Engineers.UploadEngineerDraft;

public sealed class ClaudeFolderZipReaderTests
{
    [Fact]
    public void Read_ShouldReturnFilesWithNormalizedPaths_WhenZipIsValid()
    {
        using var stream = ZipFixtureFactory.AsStream(ZipFixtureFactory.Build(("skills/a/SKILL.md", "skill body"), ("CLAUDE.md", "house rules")));

        var files = ClaudeFolderZipReader.Read(stream, UploadsOptionsFactory.Default());

        files.Select(file => file.Path).Should().Equal("skills/a/SKILL.md", "CLAUDE.md");
        Encoding.UTF8.GetString(files[0].Content).Should().Be("skill body");
    }

    [Fact]
    public void Read_ShouldSkipDirectoryEntries_WhenZipContainsFolderEntries()
    {
        using var stream = ZipFixtureFactory.AsStream(ZipFixtureFactory.Build(("skills/", ""), ("skills/a/", ""), ("skills/a/SKILL.md", "skill body")));

        var files = ClaudeFolderZipReader.Read(stream, UploadsOptionsFactory.Default());

        files.Select(file => file.Path).Should().Equal("skills/a/SKILL.md");
    }

    [Fact]
    public void Read_ShouldThrowZipInvalid_WhenStreamIsNotAZip()
    {
        using var stream = ZipFixtureFactory.AsStream(Encoding.UTF8.GetBytes("this is not a zip archive"));

        var act = () => ClaudeFolderZipReader.Read(stream, UploadsOptionsFactory.Default());

        act.Should().Throw<BadRequestCoreException>().Where(x => x.ErrorCode == ErrorCodes.UploadZipInvalid);
    }

    [Fact]
    public void Read_ShouldThrowTooManyFiles_WhenFileCountExceedsCap()
    {
        using var stream = ZipFixtureFactory.AsStream(ZipFixtureFactory.Build(("a.md", "a"), ("b.md", "b"), ("c.md", "c")));

        var act = () => ClaudeFolderZipReader.Read(stream, UploadsOptionsFactory.Default(maxFileCount: 2));

        act.Should().Throw<BadRequestCoreException>().Where(x => x.ErrorCode == ErrorCodes.UploadTooManyFiles);
    }

    [Fact]
    public void Read_ShouldThrowUncompressedTooLarge_WhenContentExceedsCap()
    {
        using var stream = ZipFixtureFactory.AsStream(ZipFixtureFactory.Build(("a.md", new string('a', 64))));

        var act = () => ClaudeFolderZipReader.Read(stream, UploadsOptionsFactory.Default(maxUncompressedSizeBytes: 8));

        act.Should().Throw<BadRequestCoreException>().Where(x => x.ErrorCode == ErrorCodes.UploadUncompressedTooLarge);
    }

    [Fact]
    public void Read_ShouldThrowUnsafePath_WhenEntryContainsParentSegment()
    {
        using var stream = ZipFixtureFactory.AsStream(ZipFixtureFactory.Build(("../evil.md", "evil")));

        var act = () => ClaudeFolderZipReader.Read(stream, UploadsOptionsFactory.Default());

        act.Should().Throw<BadRequestCoreException>().Where(x => x.ErrorCode == ErrorCodes.UploadUnsafePath);
    }

    [Fact]
    public void Read_ShouldThrowUnsafePath_WhenEntryPathIsRooted()
    {
        using var stream = ZipFixtureFactory.AsStream(ZipFixtureFactory.Build(("/abs/evil.md", "evil")));

        var act = () => ClaudeFolderZipReader.Read(stream, UploadsOptionsFactory.Default());

        act.Should().Throw<BadRequestCoreException>().Where(x => x.ErrorCode == ErrorCodes.UploadUnsafePath);
    }

    [Fact]
    public void Read_ShouldThrowSymlinkNotAllowed_WhenEntryIsSymlink()
    {
        using var stream = ZipFixtureFactory.AsStream(ZipFixtureFactory.BuildWithExternalAttributes("link.md", "../secret", unchecked((int)0xA1FF0000)));

        var act = () => ClaudeFolderZipReader.Read(stream, UploadsOptionsFactory.Default());

        act.Should().Throw<BadRequestCoreException>().Where(x => x.ErrorCode == ErrorCodes.UploadSymlinkNotAllowed);
    }
}
