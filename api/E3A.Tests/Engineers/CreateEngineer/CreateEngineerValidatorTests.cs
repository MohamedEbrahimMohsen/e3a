using E3A.Application.Engineers.CreateEngineer;
using E3A.Application.Exceptions;
using E3A.Tests.Engineers.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace E3A.Tests.Engineers.CreateEngineer;

public sealed class CreateEngineerValidatorTests
{
    private readonly CreateEngineerValidator _sut = new(Options.Create(EngineerFactory.CreateEngineersOptions()));

    [Fact]
    public void Validate_ShouldPass_WhenCommandIsValid()
    {
        var result = _sut.Validate(new CreateEngineerCommand("Dive Backend Engineer", "A backend engineer.", ["dotnet", "ddd"]));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ShouldFail_WhenDisplayNameIsMissing(string? displayName)
    {
        var result = _sut.Validate(new CreateEngineerCommand(displayName!, null, []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.EngineerDisplayNameRequired);
    }

    [Fact]
    public void Validate_ShouldFail_WhenDisplayNameExceedsMaxLength()
    {
        var result = _sut.Validate(new CreateEngineerCommand(new string('a', 101), null, []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.EngineerDisplayNameTooLong);
    }

    [Fact]
    public void Validate_ShouldFail_WhenDisplayNameHasNoAsciiLetterOrDigit()
    {
        var result = _sut.Validate(new CreateEngineerCommand("مهندس", null, []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.EngineerDisplayNameInvalid);
    }

    [Fact]
    public void Validate_ShouldFail_WhenDescriptionExceedsMaxLength()
    {
        var result = _sut.Validate(new CreateEngineerCommand("Dive Backend Engineer", new string('a', 501), []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.EngineerDescriptionTooLong);
    }

    [Fact]
    public void Validate_ShouldFail_WhenTagCountExceedsMaximum()
    {
        var tags = Enumerable.Range(0, 11).Select(index => $"tag-{index}").ToList();

        var result = _sut.Validate(new CreateEngineerCommand("Dive Backend Engineer", null, tags));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.EngineerTooManyTags);
    }

    [Fact]
    public void Validate_ShouldFail_WhenATagIsEmpty()
    {
        var result = _sut.Validate(new CreateEngineerCommand("Dive Backend Engineer", null, ["dotnet", "  "]));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.EngineerTagRequired);
    }

    [Fact]
    public void Validate_ShouldFail_WhenATagExceedsMaxLength()
    {
        var result = _sut.Validate(new CreateEngineerCommand("Dive Backend Engineer", null, [new string('a', 31)]));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.EngineerTagTooLong);
    }
}
