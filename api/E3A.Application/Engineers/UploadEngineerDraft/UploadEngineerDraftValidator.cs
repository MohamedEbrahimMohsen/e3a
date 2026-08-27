using Core.Validation.Extensions;
using E3A.Application.Exceptions;
using E3A.Application.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace E3A.Application.Engineers.UploadEngineerDraft;

public sealed class UploadEngineerDraftValidator : AbstractValidator<UploadEngineerDraftCommand>
{
    // The endpoint contract accepts exactly one archive format.
    private static readonly List<string> ZipExtensions = [".zip"];

    public UploadEngineerDraftValidator(IOptions<UploadsOptions> uploadsOptions)
    {
        var options = uploadsOptions.Value;

        RuleFor(x => x.EngineerId).ValidateRequired(ErrorCodes.EngineerIdRequired);

        RuleFor(x => x.File)
            .ValidateRequired(ErrorCodes.UploadFileRequired)
            .ValidateAllowedExtensions(ZipExtensions, ErrorCodes.UploadFileMustBeZip)
            .ValidateMaxFileSize(options.MaxZipSizeMegabytes, ErrorCodes.UploadFileTooLarge);
    }
}
