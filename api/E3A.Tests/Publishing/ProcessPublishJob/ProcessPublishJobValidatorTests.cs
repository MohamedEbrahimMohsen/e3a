using E3A.Application.Exceptions;
using E3A.Application.Publishing.ProcessPublishJob;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Publishing.ProcessPublishJob;

public sealed class ProcessPublishJobValidatorTests
{
    private readonly ProcessPublishJobValidator _sut = new();

    [Fact]
    public void Validate_ShouldPass_WhenCommandIsValid()
    {
        _sut.Validate(new ProcessPublishJobCommand(Guid.NewGuid())).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenVersionIdIsEmpty()
    {
        var result = _sut.Validate(new ProcessPublishJobCommand(Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.PublishVersionIdRequired);
    }
}
