using System.Text;
using E3A.Application.Exceptions;
using E3A.Application.Publishing.Shared;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Publishing.Shared;

public sealed class PluginStructureValidatorDuplicatePathTests
{
    [Fact]
    public void Validate_ShouldReturnDuplicatePathError_WhenTwoFilesShareAPath()
    {
        var files = Files("agents/reviewer.md", "agents/reviewer.md");

        var errors = PluginStructureValidator.Validate(files, PublishingOptionsFactory.Default());

        errors.Should().Contain(ErrorCodes.PluginDuplicatePath);
    }

    [Fact]
    public void Validate_ShouldReturnDuplicatePathError_WhenTwoFilePathsDifferOnlyByCase()
    {
        var files = Files("agents/reviewer.md", "agents/Reviewer.md");

        var errors = PluginStructureValidator.Validate(files, PublishingOptionsFactory.Default());

        errors.Should().Contain(ErrorCodes.PluginDuplicatePath);
    }

    [Fact]
    public void Validate_ShouldReturnNoDuplicatePathError_WhenAllPathsAreDistinct()
    {
        var files = Files("agents/reviewer.md", "agents/builder.md");

        var errors = PluginStructureValidator.Validate(files, PublishingOptionsFactory.Default());

        errors.Should().NotContain(ErrorCodes.PluginDuplicatePath);
    }

    private static List<PluginFile> Files(params string[] paths)
        => [.. paths.Select(x => new PluginFile(x, Encoding.UTF8.GetBytes("content")))];
}
