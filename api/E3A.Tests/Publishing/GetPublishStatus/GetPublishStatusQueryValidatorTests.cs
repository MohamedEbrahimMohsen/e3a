using E3A.Application.Exceptions;
using E3A.Application.Publishing.GetPublishStatus;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Publishing.GetPublishStatus;

public sealed class GetPublishStatusQueryValidatorTests
{
    private readonly GetPublishStatusQueryValidator _sut = new();

    [Fact]
    public void Validate_ShouldPass_WhenQueryIsValid()
    {
        var result = _sut.Validate(new GetPublishStatusQuery(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenVersionIdIsEmpty()
    {
        var result = _sut.Validate(new GetPublishStatusQuery(Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.PublishVersionIdRequired);
    }
}
