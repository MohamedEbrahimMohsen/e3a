using Core.Errors;
using E3A.Application.Engineers.Shared;
using E3A.Application.Exceptions;
using E3A.Application.Options;

namespace E3A.Application.Engineers.UploadEngineerDraft;

public sealed record NormalizedDraft(List<UploadedFile> Assets, ImportManifestResult Manifest);

public static class DraftNormalizer
{
    public const string NoPluginEquivalentReason = "No plugin equivalent.";
    public const string SkillMissingSkillFileReason = "Skill folders must contain SKILL.md at their root.";
    public const string NotConvertibleReason = "Only markdown and text content is merged into the house-rules skill.";

    // Plugin-format paths and roots (docs/plugin-spec.md): the installer resolves these exact names.
    private const string SkillsRootName = "skills";
    private const string SkillFileName = "SKILL.md";
    private const string SettingsFilePath = "settings.json";
    private const string SettingsHooksSource = $"{SettingsFilePath}#hooks";
    private const string McpFilePath = ".mcp.json";
    private const string LspFilePath = ".lsp.json";
    private const string ClaudeFilePath = "CLAUDE.md";
    private const string HooksFilePath = "hooks/hooks.json";
    private const string HouseRulesFolderName = "house-rules";
    private const string PrefixedHouseRulesFolderName = "e3a-house-rules";

    private static readonly Dictionary<string, string> ImportedRootCategories = new(StringComparer.OrdinalIgnoreCase) { ["agents"] = ImportCategories.Agents, ["commands"] = ImportCategories.Commands, ["hooks"] = ImportCategories.Hooks, ["output-styles"] = ImportCategories.OutputStyles, ["monitors"] = ImportCategories.Monitors, ["bin"] = ImportCategories.Bin, ["themes"] = ImportCategories.Themes };
    private static readonly HashSet<string> HouseRuleRootNames = new(StringComparer.OrdinalIgnoreCase) { "rules", "conventions", "docs" };
    private static readonly HashSet<string> HouseRuleExtensions = new(StringComparer.OrdinalIgnoreCase) { ".md", ".markdown", ".txt" };

    public static NormalizedDraft Normalize(List<UploadedFile> files, List<string> strippedPaths, UploadsOptions options, DateTimeOffset uploadedAt)
    {
        List<ImportedItemResult> imported = [];
        List<SkippedItemResult> skipped = [];
        List<UploadedFile> assets = [];
        List<UploadedFile> houseRuleSources = [];
        var skillFolders = SkillFoldersWithSkillFile(files);

        foreach (var file in files.Where(file => !IsSettingsFile(file.Path)))
        {
            if (IsHouseRuleSource(file.Path))
            {
                houseRuleSources.Add(file);
                continue;
            }

            var (category, reason) = Classify(file.Path, skillFolders, options);

            if (category == null)
            {
                skipped.Add(new SkippedItemResult(file.Path, reason));
                continue;
            }

            assets.Add(file);
            imported.Add(new ImportedItemResult(file.Path, file.Path, category));
        }

        var settings = ImportSettings(files);
        skipped.AddRange(settings.Skipped);

        if (settings.HooksFile != null)
        {
            assets.Add(settings.HooksFile);
            imported.Add(new ImportedItemResult(SettingsHooksSource, settings.HooksFile.Path, ImportCategories.Hooks));
        }

        var houseRules = GenerateHouseRules(files, houseRuleSources);

        if (houseRules != null)
        {
            assets.Add(houseRules.SkillFile);
        }

        if (assets.Count == 0)
        {
            throw new BadRequestCoreException(ErrorCodes.UploadEmpty);
        }

        return new NormalizedDraft(assets, new ImportManifestResult(imported, houseRules?.Converted ?? [], skipped, strippedPaths, settings.HookWarnings, houseRules?.ClaudeMdSnippet, uploadedAt));
    }

    private static SettingsImport ImportSettings(List<UploadedFile> files)
    {
        var settingsFile = files.FirstOrDefault(file => IsSettingsFile(file.Path));
        var hooksFileUploaded = files.Any(file => file.Path.Equals(HooksFilePath, StringComparison.OrdinalIgnoreCase));

        return settingsFile == null ? new SettingsImport(null, [], []) : SettingsJsonImporter.Import(settingsFile, hooksFileUploaded);
    }

    private static HouseRulesGeneration? GenerateHouseRules(List<UploadedFile> files, List<UploadedFile> sources)
    {
        if (sources.Count == 0)
        {
            return null;
        }

        var ordered = sources
            .OrderBy(source => source.Path.Equals(ClaudeFilePath, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(source => source.Path, StringComparer.Ordinal)
            .ToList();
        var collides = files.Any(file => file.Path.StartsWith($"{SkillsRootName}/{HouseRulesFolderName}/", StringComparison.OrdinalIgnoreCase));

        return HouseRulesSkillGenerator.Generate(ordered, collides ? PrefixedHouseRulesFolderName : HouseRulesFolderName);
    }

    private static (string? Category, string Reason) Classify(string path, HashSet<string> skillFolders, UploadsOptions options)
    {
        var root = RootSegment(path);

        return path switch
        {
            _ when root.Equals(SkillsRootName, StringComparison.OrdinalIgnoreCase) => skillFolders.Contains(SkillFolderOf(path)) ? (ImportCategories.Skills, string.Empty) : (null, SkillMissingSkillFileReason),
            _ when path.Equals(McpFilePath, StringComparison.OrdinalIgnoreCase) => (ImportCategories.McpServers, string.Empty),
            _ when path.Equals(LspFilePath, StringComparison.OrdinalIgnoreCase) => (ImportCategories.LspServers, string.Empty),
            _ when ImportedRootCategories.TryGetValue(root, out var category) => (category, string.Empty),
            _ when HouseRuleRootNames.Contains(root) => (null, NotConvertibleReason),
            _ when options.HookScriptExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase) => (ImportCategories.HookScripts, string.Empty),
            _ => (null, NoPluginEquivalentReason),
        };
    }

    private static bool IsSettingsFile(string path) => path.Equals(SettingsFilePath, StringComparison.OrdinalIgnoreCase);

    private static bool IsHouseRuleSource(string path) => (path.Equals(ClaudeFilePath, StringComparison.OrdinalIgnoreCase) || HouseRuleRootNames.Contains(RootSegment(path))) && HouseRuleExtensions.Contains(Path.GetExtension(path));

    private static HashSet<string> SkillFoldersWithSkillFile(List<UploadedFile> files) => new(files.Select(file => file.Path.Split('/')).Where(IsSkillFileSegments).Select(segments => segments[1]), StringComparer.OrdinalIgnoreCase);

    private static bool IsSkillFileSegments(string[] segments) => segments.Length == 3 && segments[0].Equals(SkillsRootName, StringComparison.OrdinalIgnoreCase) && segments[2].Equals(SkillFileName, StringComparison.OrdinalIgnoreCase);

    private static string SkillFolderOf(string path) => path.Split('/') is { Length: >= 3 } segments ? segments[1] : string.Empty;

    private static string RootSegment(string path) => path.IndexOf('/') is var index && index > 0 ? path[..index] : string.Empty;
}
