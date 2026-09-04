using System.Buffers;
using System.Text.Unicode;

namespace E3A.Application.Publishing.Security;

public static class PluginFileText
{
    private const char ByteOrderMark = '\uFEFF';
    private const byte NullByte = 0x00;

    public static string? TryDecode(byte[] content)
    {
        if (content.Length == 0)
        {
            return string.Empty;
        }

        if (Array.IndexOf(content, NullByte) >= 0)
        {
            return null;
        }

        var buffer = new char[content.Length];
        var status = Utf8.ToUtf16(content, buffer, out _, out var charsWritten, replaceInvalidSequences: false, isFinalBlock: true);

        return status == OperationStatus.Done ? new string(buffer, 0, charsWritten).TrimStart(ByteOrderMark) : null;
    }

    public static string[] SplitLines(string text)
    {
        return [.. text.Split('\n').Select(x => x.TrimEnd('\r'))];
    }

    public static string Excerpt(string line, int maxLength)
    {
        var trimmed = line.Trim();

        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
