using Core.Validation.Extensions;
using E3A.Application.Exceptions;
using FluentValidation;

namespace E3A.Application.Engineers.RelistEngineer;

public sealed class RelistEngineerValidator : AbstractValidator<RelistEngineerCommand>
{
    public RelistEngineerValidator()
    {
        RuleFor(x => x.EngineerId).ValidateRequired(ErrorCodes.EngineerIdRequired);
    }
}
