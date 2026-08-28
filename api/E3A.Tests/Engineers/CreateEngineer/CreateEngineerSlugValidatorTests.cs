using E3A.Application.Engineers.CreateEngineer;
using E3A.Application.Exceptions;
using E3A.Tests.Engineers.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace E3A.Tests.Engineers.CreateEngineer;

public sealed class CreateEngineerSlugValidatorTests
{
    private readonly CreateEngineerValidator _sut = new(Options.Create(EngineerFactory.CreateEngineersOptions()));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ShouldFail_WhenSlugIsMissing(string? slug)
    {
        var result = _sut.Validate(new CreateEngineerCommand(slug!, EngineerFactory.DefaultDisplayName, null, []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.EngineerSlugRequired);
    }

    [Fact]
    public void Validate_ShouldFail_WhenSlugIsShorterThanMinimum()
    {
        var result = _sut.Validate(new CreateEngineerCommand("ab", EngineerFactory.DefaultDisplayName, null, []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.EngineerSlugTooShort);
    }

    [Fact]
    public void Validate_ShouldFail_WhenSlugExceedsMaxLength()
    {
        var result = _sut.Validate(new CreateEngineerCommand(new string('a', 101), EngineerFactory.DefaultDisplayName, null, []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.EngineerSlugTooLong);
    }

    [Theory]
    [InlineData("-mmohsen")]
    [InlineData("mmohsen-")]
    [InlineData("m--mohsen")]
    [InlineData("m_mohsen")]
    [InlineData("m mohsen")]
    [InlineData("mmohsen!")]
    public void Validate_ShouldFail_WhenSlugIsNotKebabCase(string slug)
    {
        var result = _sut.Validate(new CreateEngineerCommand(slug, EngineerFactory.DefaultDisplayName, null, []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.EngineerSlugInvalid);
    }

    [Theory]
    [InlineData(EngineerFactory.DefaultReservedSlug)]
    [InlineData("API")]
    [InlineData("Marketplace")]
    public void Validate_ShouldFail_WhenSlugIsReserved(string slug)
    {
        var result = _sut.Validate(new CreateEngineerCommand(slug, EngineerFactory.DefaultDisplayName, null, []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.EngineerSlugReserved);
    }

    [Fact]
    public void Validate_ShouldPass_WhenSlugDiffersOnlyByCaseOrWhitespace()
    {
        var result = _sut.Validate(new CreateEngineerCommand("  MMohsen  ", EngineerFactory.DefaultDisplayName, null, []));

        result.IsValid.Should().BeTrue();
    }
}
