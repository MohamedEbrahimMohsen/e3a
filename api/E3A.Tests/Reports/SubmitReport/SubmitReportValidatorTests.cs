using E3A.Application.Exceptions;
using E3A.Application.Reports.SubmitReport;
using E3A.Domain.Publishing;
using E3A.Domain.Reports;
using E3A.Tests.Reports.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace E3A.Tests.Reports.SubmitReport;

public sealed class SubmitReportValidatorTests
{
    private readonly SubmitReportValidator _sut = new(Options.Create(ReportFactory.CreateReportsOptions()));

    [Fact]
    public void Validate_ShouldPass_WhenCommandIsValid()
    {
        _sut.Validate(new SubmitReportCommand(ItemType.Engineer, Guid.NewGuid(), ReportReason.Malicious, ReportFactory.DefaultDetails)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldPass_WhenDetailsAreOmittedForANonOtherReason()
    {
        _sut.Validate(new SubmitReportCommand(ItemType.Engineer, Guid.NewGuid(), ReportReason.Spam, null)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenItemIdIsEmpty()
    {
        var result = _sut.Validate(new SubmitReportCommand(ItemType.Engineer, Guid.Empty, ReportReason.Malicious, ReportFactory.DefaultDetails));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.ReportItemIdRequired);
    }

    [Fact]
    public void Validate_ShouldFail_WhenItemTypeIsNotAKnownValue()
    {
        var result = _sut.Validate(new SubmitReportCommand((ItemType)99, Guid.NewGuid(), ReportReason.Malicious, ReportFactory.DefaultDetails));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.ReportItemTypeInvalid);
    }

    [Fact]
    public void Validate_ShouldFail_WhenReasonIsNotAKnownValue()
    {
        var result = _sut.Validate(new SubmitReportCommand(ItemType.Engineer, Guid.NewGuid(), (ReportReason)99, ReportFactory.DefaultDetails));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.ReportReasonInvalid);
    }

    [Fact]
    public void Validate_ShouldFail_WhenDetailsExceedTheConfiguredMaximum()
    {
        var detailsMaxLength = ReportFactory.CreateReportsOptions().DetailsMaxLength;

        var result = _sut.Validate(new SubmitReportCommand(ItemType.Engineer, Guid.NewGuid(), ReportReason.Malicious, new string('x', detailsMaxLength + 1)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.ReportDetailsTooLong);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ShouldFail_WhenDetailsAreMissingForTheOtherReason(string? details)
    {
        var result = _sut.Validate(new SubmitReportCommand(ItemType.Engineer, Guid.NewGuid(), ReportReason.Other, details));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.ReportDetailsRequired);
    }
}
