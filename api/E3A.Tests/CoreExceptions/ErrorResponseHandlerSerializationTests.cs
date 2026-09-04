using Core.Exceptions;
using Core.Localization;
using E3A.Tests.CoreExceptions.Shared;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using System.Text.Json;
using Xunit;

namespace E3A.Tests.CoreExceptions;

public sealed class ErrorResponseHandlerSerializationTests
{
    private const string LocalizedMessage = "localized";

    private static readonly JsonSerializerOptions ErrorSerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ILocalizer _localizer = Substitute.For<ILocalizer>();
    private readonly IHostEnvironment _environment = Substitute.For<IHostEnvironment>();
    private readonly ErrorResponseHandler _sut;

    public ErrorResponseHandlerSerializationTests()
    {
        _localizer.GetMessage(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Dictionary<string, object>?>()).Returns(LocalizedMessage);
        _sut = new ErrorResponseHandler(_localizer, _environment);
    }

    [Fact]
    public void Serialize_ShouldEmitOnlyCodeAndMessage_WhenEnvironmentIsNotDevelopment()
    {
        _environment.EnvironmentName.Returns("Production");
        var details = ExceptionDetailsFactory.Thrown();

        var json = JsonSerializer.Serialize(_sut.GenerateErrorResponse(details), ErrorSerializerOptions);
        using var document = JsonDocument.Parse(json);

        document.RootElement.TryGetProperty("data", out _).Should().BeFalse();
        document.RootElement.TryGetProperty("code", out _).Should().BeTrue();
        document.RootElement.TryGetProperty("message", out _).Should().BeTrue();
        document.RootElement.EnumerateObject().Should().HaveCount(2);
    }

    [Fact]
    public void Serialize_ShouldEmitDiagnosticsInData_WhenEnvironmentIsDevelopment()
    {
        _environment.EnvironmentName.Returns(Environments.Development);
        var details = ExceptionDetailsFactory.Thrown();

        var json = JsonSerializer.Serialize(_sut.GenerateErrorResponse(details), ErrorSerializerOptions);
        using var document = JsonDocument.Parse(json);

        document.RootElement.TryGetProperty("data", out var data).Should().BeTrue();
        data.GetString().Should().Contain(details.Exception!.StackTrace!);
    }
}
