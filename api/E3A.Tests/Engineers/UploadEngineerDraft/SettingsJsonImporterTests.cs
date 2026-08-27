using System.Text;
using System.Text.Json;
using E3A.Application.Engineers.Shared;
using E3A.Application.Engineers.UploadEngineerDraft;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Engineers.UploadEngineerDraft;

public sealed class SettingsJsonImporterTests
{
    [Fact]
    public void Import_ShouldProduceHooksFileAndWarnings_WhenHooksSectionIsWellFormed()
    {
        var settings = Settings("""{"hooks":{"PreToolUse":[{"matcher":"Bash","hooks":[{"type":"command","command":"echo hi"}]}]}}""");

        var import = SettingsJsonImporter.Import(settings, hooksFileAlreadyUploaded: false);

        import.HooksFile!.Path.Should().Be("hooks/hooks.json");
        using var document = JsonDocument.Parse(import.HooksFile.Content);
        document.RootElement.TryGetProperty("hooks", out _).Should().BeTrue();
        import.HookWarnings.Should().ContainSingle().Which.Should().Be(new HookWarningResult("PreToolUse", "Bash", "echo hi"));
    }

    [Fact]
    public void Import_ShouldWarnPerEventWithoutCommand_WhenHookShapeIsUnrecognized()
    {
        var settings = Settings("""{"hooks":{"PreToolUse":"echo hi"}}""");

        var import = SettingsJsonImporter.Import(settings, hooksFileAlreadyUploaded: false);

        import.HookWarnings.Should().ContainSingle().Which.Should().Be(new HookWarningResult("PreToolUse", null, null));
    }

    [Fact]
    public void Import_ShouldSkipKnownSettingsKeys_WithReasons()
    {
        var settings = Settings("""{"permissions":{},"env":{},"model":"opus","statusLine":{}}""");

        var import = SettingsJsonImporter.Import(settings, hooksFileAlreadyUploaded: false);

        import.HooksFile.Should().BeNull();
        import.Skipped.Should().Equal(
            new SkippedItemResult("settings.json#permissions", SettingsJsonImporter.PermissionsSkippedReason),
            new SkippedItemResult("settings.json#env", SettingsJsonImporter.EnvironmentSkippedReason),
            new SkippedItemResult("settings.json#model", SettingsJsonImporter.ModelSkippedReason),
            new SkippedItemResult("settings.json#statusLine", SettingsJsonImporter.StatuslineSkippedReason));
    }

    [Fact]
    public void Import_ShouldReturnSkippedOnly_WhenJsonIsInvalid()
    {
        var settings = Settings("not json at all");

        var import = SettingsJsonImporter.Import(settings, hooksFileAlreadyUploaded: false);

        import.HooksFile.Should().BeNull();
        import.HookWarnings.Should().BeEmpty();
        import.Skipped.Should().ContainSingle().Which.Should().Be(new SkippedItemResult("settings.json", SettingsJsonImporter.SettingsUnparseableReason));
    }

    [Fact]
    public void Import_ShouldSkipHooksSection_WhenHooksFileAlreadyUploaded()
    {
        var settings = Settings("""{"hooks":{"PreToolUse":[]}}""");

        var import = SettingsJsonImporter.Import(settings, hooksFileAlreadyUploaded: true);

        import.HooksFile.Should().BeNull();
        import.Skipped.Should().ContainSingle().Which.Should().Be(new SkippedItemResult("settings.json#hooks", SettingsJsonImporter.HooksAlreadyUploadedReason));
    }

    [Fact]
    public void Import_ShouldSkipHooksSection_WhenHooksValueIsNotAnObject()
    {
        var settings = Settings("""{"hooks":"x"}""");

        var import = SettingsJsonImporter.Import(settings, hooksFileAlreadyUploaded: false);

        import.HooksFile.Should().BeNull();
        import.HookWarnings.Should().BeEmpty();
        import.Skipped.Should().ContainSingle().Which.Should().Be(new SkippedItemResult("settings.json#hooks", SettingsJsonImporter.HooksNotConvertibleReason));
    }

    private static UploadedFile Settings(string content)
    {
        return new UploadedFile("settings.json", Encoding.UTF8.GetBytes(content));
    }
}
