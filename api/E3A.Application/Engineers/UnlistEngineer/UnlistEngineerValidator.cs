using Core.Validation.Extensions;
using E3A.Application.Exceptions;
using FluentValidation;

namespace E3A.Application.Engineers.UnlistEngineer;

public sealed class UnlistEngineerValidator : AbstractValidator<UnlistEngineerCommand>
{
    public UnlistEngineerValidator()
    {
        RuleFor(x => x.EngineerId).ValidateRequired(ErrorCodes.EngineerIdRequired);
    }
}
