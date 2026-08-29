using E3A.Application.Engineers.UpdateEngineer;
using E3A.Application.Exceptions;
using E3A.Tests.Engineers.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace E3A.Tests.Engineers.UpdateEngineer;

public sealed class UpdateEngineerSlugValidatorTests
{
    private readonly UpdateEngineerValidator _sut = new(Options.Create(EngineerFactory.CreateEngineersOptions()));

    [Fact]
    public void Validate_ShouldPass_WhenSlugIsNull()
    {
        var result = _sut.Validate(new UpdateEngineerCommand(Guid.NewGuid(), null, EngineerFactory.DefaultDisplayName, null, []));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ShouldFail_WhenSlugIsBlank(string slug)
    {
        var result = _sut.Validate(new UpdateEngineerCommand(Guid.NewGuid(), slug, EngineerFactory.DefaultDisplayName, null, []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.EngineerSlugRequired);
    }

    [Fact]
    public void Validate_ShouldFail_WhenSlugIsShorterThanMinimum()
    {
        var result = _sut.Validate(new UpdateEngineerCommand(Guid.NewGuid(), "ab", EngineerFactory.DefaultDisplayName, null, []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.EngineerSlugTooShort);
    }

    [Fact]
    public void Validate_ShouldFail_WhenSlugExceedsMaxLength()
    {
        var result = _sut.Validate(new UpdateEngineerCommand(Guid.NewGuid(), new string('a', 101), EngineerFactory.DefaultDisplayName, null, []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.EngineerSlugTooLong);
    }

    [Theory]
    [InlineData("-mmohsen")]
    [InlineData("mmohsen-")]
    [InlineData("m--mohsen")]
    [InlineData("m mohsen")]
    public void Validate_ShouldFail_WhenSlugIsNotKebabCase(string slug)
    {
        var result = _sut.Validate(new UpdateEngineerCommand(Guid.NewGuid(), slug, EngineerFactory.DefaultDisplayName, null, []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.EngineerSlugInvalid);
    }

    [Fact]
    public void Validate_ShouldFail_WhenSlugIsReserved()
    {
        var result = _sut.Validate(new UpdateEngineerCommand(Guid.NewGuid(), EngineerFactory.DefaultReservedSlug, EngineerFactory.DefaultDisplayName, null, []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.EngineerSlugReserved);
    }

    [Fact]
    public void Validate_ShouldFail_WhenSlugUsesTheTeamNamespacePrefix()
    {
        var result = _sut.Validate(new UpdateEngineerCommand(Guid.NewGuid(), "team-alpha", EngineerFactory.DefaultDisplayName, null, []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.EngineerSlugReserved);
    }

    [Fact]
    public void Validate_ShouldPass_WhenSlugDiffersOnlyByCaseOrWhitespace()
    {
        var result = _sut.Validate(new UpdateEngineerCommand(Guid.NewGuid(), "  MMohsen  ", EngineerFactory.DefaultDisplayName, null, []));

        result.IsValid.Should().BeTrue();
    }
}
