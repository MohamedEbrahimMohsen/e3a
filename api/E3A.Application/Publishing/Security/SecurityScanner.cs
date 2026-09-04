using E3A.Application.Options;
using E3A.Application.Publishing.Shared;

namespace E3A.Application.Publishing.Security;

public static class SecurityScanner
{
    // A finding on line 0 is file-level; the creator-facing report renders it as "whole file" rather than a line reference.
    public const int FileLevelLine = 0;

    public static ScanReport Scan(List<PluginFile> files, List<string> scriptExtensions, PublishingOptions options)
    {
        List<ScanFinding> findings = [];
        var hookScriptCount = 0;
        var scannedFileCount = 0;

        foreach (var file in files.OrderBy(x => x.Path, StringComparer.Ordinal))
        {
            var isScript = scriptExtensions.Contains(Path.GetExtension(file.Path), StringComparer.OrdinalIgnoreCase);

            if (isScript)
            {
                hookScriptCount++;
            }

            findings.AddRange(HygieneRules.Inspect(file, options));

            var text = PluginFileText.TryDecode(file.Content);

            if (text == null)
            {
                continue;
            }

            scannedFileCount++;
            findings.AddRange(ScanLines(file.Path, text, isScript, options));
        }

        return Report(findings, hookScriptCount, scannedFileCount, options);
    }

    private static List<ScanFinding> ScanLines(string path, string text, bool isScript, PublishingOptions options)
    {
        var lines = PluginFileText.SplitLines(text);
        var rules = ScanRuleCatalogue.RulesFor(isScript);
        List<ScanFinding> findings = [];

        for (var index = 0; index < lines.Length; index++)
        {
            // Scan cost tracks candidate token pairs, not length, and three attempts to predict cost from a line's shape were each broken by a denser adversarial unit. Shape is a proxy for cost and proxies lose to crafted input, so every over-length line takes the block path instead.
            if (lines[index].Length > options.ScanMaxLineLength)
            {
                findings.Add(HygieneRules.LineOverCap(path, lines[index], index + 1, options));
                continue;
            }

            findings.AddRange(rules
                .Where(rule => rule.Pattern.IsMatch(lines[index]))
                .Select(rule => new ScanFinding(rule.RuleId, rule.Category, rule.SeverityFor(isScript), path, index + 1, PluginFileText.Excerpt(lines[index], options.ScanExcerptMaxLength))));
        }

        return findings;
    }

    private static ScanReport Report(List<ScanFinding> findings, int hookScriptCount, int scannedFileCount, PublishingOptions options)
    {
        List<ScanFinding> ordered = [.. findings
            .OrderByDescending(x => x.Severity)
            .ThenBy(x => x.Path, StringComparer.Ordinal)
            .ThenBy(x => x.Line)
            .ThenBy(x => x.RuleId, StringComparer.Ordinal)];

        var isTruncated = ordered.Count > options.MaxScanFindings;

        return new ScanReport(isTruncated ? [.. ordered.Take(options.MaxScanFindings)] : ordered, hookScriptCount, scannedFileCount, isTruncated);
    }
}
