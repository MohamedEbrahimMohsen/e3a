using E3A.Application.Exceptions;
using E3A.Application.Publishing.Shared;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Publishing.Shared;

public sealed class PluginStructureValidatorTests
{
    private const string AgentPath = "agents/reviewer.md";

    [Fact]
    public void Validate_ShouldReturnEmpty_WhenTreeIsWellFormed()
    {
        var files = PluginFileFactory.Files(AgentPath, "skills/house-rules/SKILL.md", PluginJsonGenerator.PluginJsonPath);

        var result = PluginStructureValidator.Validate(files, PluginFileFactory.Manifest(AgentPath, "skills/house-rules/SKILL.md"), PublishingOptionsFactory.Default());

        result.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ShouldReportManifestAssetMissing_WhenManifestTargetIsAbsent()
    {
        var files = PluginFileFactory.Files(AgentPath, PluginJsonGenerator.PluginJsonPath);

        var result = PluginStructureValidator.Validate(files, PluginFileFactory.Manifest(AgentPath, "commands/ship.md"), PublishingOptionsFactory.Default());

        result.Should().Contain(ErrorCodes.PluginManifestAssetMissing);
    }

    [Fact]
    public void Validate_ShouldReportNoInstallableContent_WhenOnlyPluginJsonExists()
    {
        var files = PluginFileFactory.Files(PluginJsonGenerator.PluginJsonPath);

        var result = PluginStructureValidator.Validate(files, PluginFileFactory.Manifest(), PublishingOptionsFactory.Default());

        result.Should().Contain(ErrorCodes.PluginNoInstallableContent);
    }

    [Theory]
    [InlineData("../x.md")]
    [InlineData("skills/../../x.md")]
    [InlineData("/agents/x.md")]
    [InlineData("agents\\x.md")]
    [InlineData("")]
    public void Validate_ShouldReportUnsafePath_WhenPathEscapes(string unsafePath)
    {
        var files = PluginFileFactory.Files(AgentPath, unsafePath);

        var result = PluginStructureValidator.Validate(files, PluginFileFactory.Manifest(), PublishingOptionsFactory.Default());

        result.Should().Contain(ErrorCodes.PluginUnsafePath);
    }

    [Fact]
    public void Validate_ShouldReportSkillMissingSkillFile_WhenSkillFolderLacksIt()
    {
        var files = PluginFileFactory.Files(AgentPath, "skills/house-rules/reference.md");

        var result = PluginStructureValidator.Validate(files, PluginFileFactory.Manifest(), PublishingOptionsFactory.Default());

        result.Should().Contain(ErrorCodes.PluginSkillMissingSkillFile);
    }

    [Fact]
    public void Validate_ShouldReportTooManyFiles_WhenCountExceedsOption()
    {
        var files = PluginFileFactory.Files(AgentPath, "commands/ship.md", PluginJsonGenerator.PluginJsonPath);

        var result = PluginStructureValidator.Validate(files, PluginFileFactory.Manifest(), PublishingOptionsFactory.Default(maxPluginFileCount: 2));

        result.Should().Contain(ErrorCodes.PluginTooManyFiles);
    }

    [Fact]
    public void Validate_ShouldReportTooLarge_WhenTotalBytesExceedOption()
    {
        var files = PluginFileFactory.Files(AgentPath);

        var result = PluginStructureValidator.Validate(files, PluginFileFactory.Manifest(), PublishingOptionsFactory.Default(maxPluginBytes: 1));

        result.Should().Contain(ErrorCodes.PluginTooLarge);
    }
}
