using Core.Validation.Extensions;
using E3A.Application.Exceptions;
using FluentValidation;

namespace E3A.Application.Engineers.GetImportManifest;

public sealed class GetImportManifestQueryValidator : AbstractValidator<GetImportManifestQuery>
{
    public GetImportManifestQueryValidator()
    {
        RuleFor(x => x.EngineerId).ValidateRequired(ErrorCodes.EngineerIdRequired);
    }
}
