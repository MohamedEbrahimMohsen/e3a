using System.Text;
using E3A.Application.Publishing.Security;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Publishing.Security;

public sealed class PluginFileTextTests
{
    [Fact]
    public void TryDecode_ShouldReturnNull_WhenContentContainsNullByte()
    {
        PluginFileText.TryDecode([0x61, 0x00, 0x62]).Should().BeNull();
    }

    [Fact]
    public void TryDecode_ShouldReturnNull_WhenContentIsInvalidUtf8()
    {
        PluginFileText.TryDecode([0xC3, 0x28]).Should().BeNull();
    }

    [Fact]
    public void TryDecode_ShouldReturnText_WhenContentIsUtf8()
    {
        const string text = "مرحبا بالعالم 🚀 ok";

        PluginFileText.TryDecode(Encoding.UTF8.GetBytes(text)).Should().Be(text);
    }

    [Fact]
    public void TryDecode_ShouldStripByteOrderMark_WhenContentHasBom()
    {
        PluginFileText.TryDecode([.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes("alpha")]).Should().Be("alpha");
    }

    [Fact]
    public void TryDecode_ShouldReturnEmpty_WhenContentIsEmpty()
    {
        PluginFileText.TryDecode([]).Should().Be(string.Empty);
    }

    [Fact]
    public void SplitLines_ShouldStripCarriageReturns_WhenTextIsCrLf()
    {
        PluginFileText.SplitLines("a\r\nb").Should().Equal("a", "b");
    }

    [Theory]
    [InlineData("   hello   ", 10, "hello")]
    [InlineData("abcdefghij", 4, "abcd")]
    [InlineData("   abcdefghij   ", 4, "abcd")]
    public void Excerpt_ShouldTrimAndTruncate_WhenLineIsLongOrPadded(string line, int maxLength, string expected)
    {
        PluginFileText.Excerpt(line, maxLength).Should().Be(expected);
    }
}
