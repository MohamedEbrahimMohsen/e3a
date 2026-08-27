using System.Text;
using System.Text.Json;
using E3A.Application.Engineers.Shared;

namespace E3A.Application.Engineers.UploadEngineerDraft;

public sealed record SettingsImport(UploadedFile? HooksFile, List<HookWarningResult> HookWarnings, List<SkippedItemResult> Skipped);

public static class SettingsJsonImporter
{
    public const string SettingsUnparseableReason = "settings.json could not be parsed.";
    public const string PermissionsSkippedReason = "Plugins cannot carry permissions; they will be shown on the detail page as recommended settings.";
    public const string EnvironmentSkippedReason = "Environment variables have no plugin equivalent.";
    public const string ModelSkippedReason = "Model selection has no plugin equivalent.";
    public const string StatuslineSkippedReason = "Statusline has no plugin equivalent.";
    public const string NoPluginEquivalentReason = "No plugin equivalent.";
    public const string HooksAlreadyUploadedReason = "The upload already contains hooks/hooks.json; the settings.json hooks section was not converted.";
    public const string HooksNotConvertibleReason = "The settings.json hooks section must be a JSON object; it was not converted.";

    // Claude settings key names and the plugin hooks file path are format contracts (docs/plugin-spec.md).
    private const string HooksKey = "hooks";
    private const string MatcherKey = "matcher";
    private const string CommandKey = "command";
    private const string HooksFilePath = "hooks/hooks.json";

    public static SettingsImport Import(UploadedFile settingsFile, bool hooksFileAlreadyUploaded)
    {
        try
        {
            using var document = JsonDocument.Parse(settingsFile.Content);
            return document.RootElement.ValueKind == JsonValueKind.Object ? ImportKeys(settingsFile, document.RootElement, hooksFileAlreadyUploaded) : Unparseable(settingsFile);
        }
        catch (JsonException)
        {
            return Unparseable(settingsFile);
        }
    }

    private static SettingsImport Unparseable(UploadedFile settingsFile) => new(null, [], [new SkippedItemResult(settingsFile.Path, SettingsUnparseableReason)]);

    private static SettingsImport ImportKeys(UploadedFile settingsFile, JsonElement root, bool hooksFileAlreadyUploaded)
    {
        UploadedFile? hooksFile = null;
        List<HookWarningResult> warnings = [];
        List<SkippedItemResult> skipped = [];

        foreach (var property in root.EnumerateObject())
        {
            var isHooksSection = property.Name.Equals(HooksKey, StringComparison.OrdinalIgnoreCase);

            if (!isHooksSection || hooksFileAlreadyUploaded || property.Value.ValueKind != JsonValueKind.Object)
            {
                skipped.Add(new SkippedItemResult($"{settingsFile.Path}#{property.Name}", SkipReasonFor(property.Name, isHooksSection, hooksFileAlreadyUploaded)));
                continue;
            }

            // Plugin hooks.json format: the hooks object is wrapped in a top-level "hooks" property.
            hooksFile = new UploadedFile(HooksFilePath, Encoding.UTF8.GetBytes($"{{\"{HooksKey}\":{property.Value.GetRawText()}}}"));
            warnings.AddRange(HookWarnings(property.Value));
        }

        return new SettingsImport(hooksFile, warnings, skipped);
    }

    private static string SkipReasonFor(string key, bool isHooksSection, bool hooksFileAlreadyUploaded)
    {
        return (isHooksSection, hooksFileAlreadyUploaded) switch
        {
            (true, true) => HooksAlreadyUploadedReason,
            (true, false) => HooksNotConvertibleReason,
            _ => ReasonFor(key),
        };
    }

    private static string ReasonFor(string key)
    {
        return key.ToLowerInvariant() switch
        {
            "permissions" => PermissionsSkippedReason,
            "env" => EnvironmentSkippedReason,
            "model" => ModelSkippedReason,
            "statusline" => StatuslineSkippedReason,
            _ => NoPluginEquivalentReason,
        };
    }

    private static List<HookWarningResult> HookWarnings(JsonElement hooks)
    {
        return hooks.ValueKind != JsonValueKind.Object
            ? []
            : [.. hooks.EnumerateObject().SelectMany(hookEvent => EventWarnings(hookEvent.Name, hookEvent.Value))];
    }

    private static List<HookWarningResult> EventWarnings(string eventName, JsonElement matchers)
    {
        return matchers.ValueKind != JsonValueKind.Array
            ? [new HookWarningResult(eventName, null, null)]
            : [.. matchers.EnumerateArray().SelectMany(matcher => MatcherWarnings(eventName, matcher))];
    }

    private static List<HookWarningResult> MatcherWarnings(string eventName, JsonElement matcherElement)
    {
        var matcher = TextProperty(matcherElement, MatcherKey);

        return matcherElement.ValueKind != JsonValueKind.Object || !matcherElement.TryGetProperty(HooksKey, out var commands) || commands.ValueKind != JsonValueKind.Array
            ? [new HookWarningResult(eventName, matcher, null)]
            : [.. commands.EnumerateArray().Select(command => new HookWarningResult(eventName, matcher, TextProperty(command, CommandKey)))];
    }

    private static string? TextProperty(JsonElement element, string name)
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }
}
