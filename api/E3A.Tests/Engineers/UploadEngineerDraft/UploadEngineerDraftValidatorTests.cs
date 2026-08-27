using E3A.Application.Engineers.UploadEngineerDraft;
using E3A.Application.Exceptions;
using E3A.Tests.Engineers.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace E3A.Tests.Engineers.UploadEngineerDraft;

public sealed class UploadEngineerDraftValidatorTests
{
    private const long OneMegabyte = 1024L * 1024L;
    private readonly UploadEngineerDraftValidator _sut = new(Options.Create(UploadsOptionsFactory.Default()));

    [Fact]
    public void Validate_ShouldPass_WhenCommandIsValid()
    {
        var result = _sut.Validate(new UploadEngineerDraftCommand(Guid.NewGuid(), ZipFile("claude.zip", OneMegabyte)));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenEngineerIdIsEmpty()
    {
        var result = _sut.Validate(new UploadEngineerDraftCommand(Guid.Empty, ZipFile("claude.zip", OneMegabyte)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorCode == ErrorCodes.EngineerIdRequired);
    }

    [Fact]
    public void Validate_ShouldFail_WhenFileIsEmpty()
    {
        var result = _sut.Validate(new UploadEngineerDraftCommand(Guid.NewGuid(), ZipFile("claude.zip", 0)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorCode == ErrorCodes.UploadFileRequired);
    }

    [Fact]
    public void Validate_ShouldFail_WhenFileIsNotZip()
    {
        var result = _sut.Validate(new UploadEngineerDraftCommand(Guid.NewGuid(), ZipFile("x.rar", OneMegabyte)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorCode == ErrorCodes.UploadFileMustBeZip);
    }

    [Fact]
    public void Validate_ShouldFail_WhenFileExceedsMaxSize()
    {
        var result = _sut.Validate(new UploadEngineerDraftCommand(Guid.NewGuid(), ZipFile("claude.zip", 21 * OneMegabyte)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorCode == ErrorCodes.UploadFileTooLarge);
    }

    private static IFormFile ZipFile(string fileName, long length)
    {
        var file = Substitute.For<IFormFile>();
        file.FileName.Returns(fileName);
        file.Length.Returns(length);
        return file;
    }
}
