using E3A.Application.Engineers.GetImportManifest;
using E3A.Application.Exceptions;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Engineers.GetImportManifest;

public sealed class GetImportManifestQueryValidatorTests
{
    private readonly GetImportManifestQueryValidator _sut = new();

    [Fact]
    public void Validate_ShouldPass_WhenQueryIsValid()
    {
        _sut.Validate(new GetImportManifestQuery(Guid.NewGuid())).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenEngineerIdIsEmpty()
    {
        var result = _sut.Validate(new GetImportManifestQuery(Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorCode == ErrorCodes.EngineerIdRequired);
    }
}
