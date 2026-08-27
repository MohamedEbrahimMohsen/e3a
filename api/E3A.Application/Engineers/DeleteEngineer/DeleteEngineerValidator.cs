using Core.Validation.Extensions;
using E3A.Application.Exceptions;
using FluentValidation;

namespace E3A.Application.Engineers.DeleteEngineer;

public sealed class DeleteEngineerValidator : AbstractValidator<DeleteEngineerCommand>
{
    public DeleteEngineerValidator()
    {
        RuleFor(x => x.EngineerId).ValidateRequired(ErrorCodes.EngineerIdRequired);
    }
}
