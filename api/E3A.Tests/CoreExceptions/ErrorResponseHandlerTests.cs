using Core.Exceptions;
using Core.Localization;
using E3A.Tests.CoreExceptions.Shared;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Xunit;

namespace E3A.Tests.CoreExceptions;

public sealed class ErrorResponseHandlerTests
{
    private const string LocalizedMessage = "localized";
    private const int ExpectedPayload = 42;

    private readonly ILocalizer _localizer = Substitute.For<ILocalizer>();
    private readonly IHostEnvironment _environment = Substitute.For<IHostEnvironment>();
    private readonly ErrorResponseHandler _sut;

    public ErrorResponseHandlerTests()
    {
        _localizer.GetMessage(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Dictionary<string, object>?>()).Returns(LocalizedMessage);
        _sut = new ErrorResponseHandler(_localizer, _environment);
    }

    [Fact]
    public void GenerateErrorResponse_ShouldIncludeExceptionDiagnostics_WhenEnvironmentIsDevelopment()
    {
        _environment.EnvironmentName.Returns(Environments.Development);
        var details = ExceptionDetailsFactory.Thrown();

        var result = _sut.GenerateErrorResponse(details);

        result.Data.Should().NotBeNull();
        result.Data.Should().Contain(details.Exception!.StackTrace!);
        result.Code.Should().Be(ExceptionDetailsFactory.Code);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("QualityAssurance")]
    public void GenerateErrorResponse_ShouldOmitExceptionDiagnostics_WhenEnvironmentIsNotDevelopment(string environmentName)
    {
        _environment.EnvironmentName.Returns(environmentName);
        var details = ExceptionDetailsFactory.Thrown();

        var result = _sut.GenerateErrorResponse(details);

        result.Data.Should().BeNull();
        result.Code.Should().Be(ExceptionDetailsFactory.Code);
    }

    [Fact]
    public void GenerateErrorResponse_ShouldKeepExplicitData_WhenEnvironmentIsNotDevelopment()
    {
        _environment.EnvironmentName.Returns("Production");

        var result = _sut.GenerateErrorResponse(ExceptionDetailsFactory.Code, string.Empty, ExpectedPayload);

        result.Data.Should().Be(ExpectedPayload);
    }
}
