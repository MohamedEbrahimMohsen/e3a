using System.Text;
using E3A.Application.Publishing.Shared;
using E3A.Tests.Engineers.Shared;

namespace E3A.Tests.Publishing.Security;

public static class ScanCorpusFactory
{
    public const string MarkdownPath = "skills/demo/SKILL.md";
    public const string BinaryPath = "assets/blob.png";
    private const string Base64Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

    public static List<string> ScriptExtensions => UploadsOptionsFactory.Default().HookScriptExtensions;

    public static List<PluginFile> Markdown(string content)
    {
        return [new PluginFile(MarkdownPath, Encoding.UTF8.GetBytes(content))];
    }

    public static List<PluginFile> Script(string content, string extension = ".sh")
    {
        return [new PluginFile($"hooks/hook{extension}", Encoding.UTF8.GetBytes(content))];
    }

    public static List<PluginFile> Binary(byte[] bytes)
    {
        return [new PluginFile(BinaryPath, bytes)];
    }

    public static string Base64Line(int length)
    {
        return string.Concat(Enumerable.Range(0, length).Select(x => Base64Alphabet[x % Base64Alphabet.Length]));
    }
}
