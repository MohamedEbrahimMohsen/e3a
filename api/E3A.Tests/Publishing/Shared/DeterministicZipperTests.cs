using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using E3A.Application.Publishing.Shared;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Publishing.Shared;

public sealed class DeterministicZipperTests
{
    private readonly List<PluginFile> _files = PluginFileFactory.Files("skills/house-rules/SKILL.md", "agents/reviewer.md", ".claude-plugin/plugin.json", "commands/ship.md");

    private static readonly DateTimeOffset DeterministicTimestamp = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_ShouldProduceIdenticalBytesAndHash_WhenCalledTwiceWithSameInput()
    {
        var first = DeterministicZipper.Create(_files);
        var second = DeterministicZipper.Create(_files);

        first.Content.Should().Equal(second.Content);
        first.Sha256.Should().Be(second.Sha256);

        using var stream = new MemoryStream(first.Content);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false, Encoding.UTF8);

        archive.Entries.Should().OnlyContain(x => x.LastWriteTime.Year == DeterministicTimestamp.Year && x.LastWriteTime.DayOfYear == DeterministicTimestamp.DayOfYear && x.LastWriteTime.TimeOfDay == DeterministicTimestamp.TimeOfDay);
    }

    [Fact]
    public void Create_ShouldProduceIdenticalBytes_WhenInputOrderDiffers()
    {
        var shuffled = _files.AsEnumerable().Reverse().ToList();

        var first = DeterministicZipper.Create(_files);
        var second = DeterministicZipper.Create(shuffled);

        first.Content.Should().Equal(second.Content);
        first.Sha256.Should().Be(second.Sha256);
    }

    [Fact]
    public void Create_ShouldRoundTripEveryEntry_WhenOpened()
    {
        var zipped = DeterministicZipper.Create(_files);

        using var stream = new MemoryStream(zipped.Content);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false, Encoding.UTF8);

        var expectedPaths = _files.Select(x => x.Path).OrderBy(x => x, StringComparer.Ordinal).ToList();
        archive.Entries.Select(x => x.FullName).Should().Equal(expectedPaths);

        foreach (var entry in archive.Entries)
        {
            using var entryStream = entry.Open();
            using var reader = new StreamReader(entryStream, Encoding.UTF8);
            reader.ReadToEnd().Should().Be($"content of {entry.FullName}");
        }
    }

    [Fact]
    public void Create_ShouldReturnLowercaseHexSha256OfContent_WhenCalled()
    {
        var zipped = DeterministicZipper.Create(_files);

        zipped.Sha256.Should().HaveLength(64);
        zipped.Sha256.Should().Be(Convert.ToHexString(SHA256.HashData(zipped.Content)).ToLowerInvariant());
        zipped.Sha256.Should().Be(zipped.Sha256.ToLowerInvariant());
        zipped.SizeBytes.Should().Be(zipped.Content.LongLength);
    }
}
