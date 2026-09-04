using E3A.Application.Options;
using E3A.Application.Publishing.Shared;

namespace E3A.Application.Publishing.Security;

public static class HygieneRules
{
    // PE (MZ), ELF, Mach-O in both endiannesses and Java class magic — a composed plugin tree never legitimately ships a native executable.
    private static readonly byte[][] ExecutableSignatures = [[0x4D, 0x5A], [0x7F, 0x45, 0x4C, 0x46], [0xFE, 0xED, 0xFA, 0xCE], [0xFE, 0xED, 0xFA, 0xCF], [0xCE, 0xFA, 0xED, 0xFE], [0xCF, 0xFA, 0xED, 0xFE], [0xCA, 0xFE, 0xBA, 0xBE]];

    public static List<ScanFinding> Inspect(PluginFile file, PublishingOptions options)
    {
        List<ScanFinding> findings = [];

        if (Array.Exists(ExecutableSignatures, signature => StartsWith(file.Content, signature)))
        {
            findings.Add(Finding(ScanRuleIds.ExecutableMagicBytes, file.Path));
        }

        if (file.Content.LongLength > options.MaxPluginFileBytes)
        {
            findings.Add(Finding(ScanRuleIds.FileOverSizeCap, file.Path));
        }

        return findings;
    }

    public static ScanFinding LineOverCap(string path, string line, int lineNumber, PublishingOptions options)
    {
        return new ScanFinding(ScanRuleIds.LineOverLengthCap, ScanCategories.Hygiene, ScanSeverity.Block, path, lineNumber, PluginFileText.Excerpt(line, options.ScanExcerptMaxLength));
    }

    private static ScanFinding Finding(string ruleId, string path)
    {
        return new ScanFinding(ruleId, ScanCategories.Hygiene, ScanSeverity.Block, path, SecurityScanner.FileLevelLine, path);
    }

    private static bool StartsWith(byte[] content, byte[] signature)
    {
        return content.Length >= signature.Length && content.AsSpan(0, signature.Length).SequenceEqual(signature);
    }
}
