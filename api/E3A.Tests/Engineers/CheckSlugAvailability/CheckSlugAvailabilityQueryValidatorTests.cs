using E3A.Application.Engineers.CheckSlugAvailability;
using E3A.Application.Exceptions;
using E3A.Tests.Engineers.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace E3A.Tests.Engineers.CheckSlugAvailability;

public sealed class CheckSlugAvailabilityQueryValidatorTests
{
    private readonly CheckSlugAvailabilityQueryValidator _sut = new(Options.Create(EngineerFactory.CreateEngineersOptions()));

    [Fact]
    public void Validate_ShouldPass_WhenSlugIsValid()
    {
        var result = _sut.Validate(new CheckSlugAvailabilityQuery("mmohsen"));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ShouldFail_WhenSlugIsMissing(string? slug)
    {
        var result = _sut.Validate(new CheckSlugAvailabilityQuery(slug!));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.EngineerSlugRequired);
    }

    [Fact]
    public void Validate_ShouldFail_WhenSlugIsShorterThanMinimum()
    {
        var result = _sut.Validate(new CheckSlugAvailabilityQuery("ab"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.EngineerSlugTooShort);
    }

    [Fact]
    public void Validate_ShouldFail_WhenSlugExceedsMaxLength()
    {
        var result = _sut.Validate(new CheckSlugAvailabilityQuery(new string('a', 101)));

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
        var result = _sut.Validate(new CheckSlugAvailabilityQuery(slug));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.EngineerSlugInvalid);
    }

    [Fact]
    public void Validate_ShouldFail_WhenSlugIsReserved()
    {
        var result = _sut.Validate(new CheckSlugAvailabilityQuery(EngineerFactory.DefaultReservedSlug));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.EngineerSlugReserved);
    }
}
