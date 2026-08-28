using Core.Validation.Extensions;
using E3A.Application.Exceptions;
using FluentValidation;

namespace E3A.Application.Engineers.PublishEngineer;

public sealed class PublishEngineerValidator : AbstractValidator<PublishEngineerCommand>
{
    public PublishEngineerValidator()
    {
        RuleFor(x => x.EngineerId).ValidateRequired(ErrorCodes.EngineerIdRequired);
        RuleFor(x => x.Increment).IsInEnum().WithErrorCode(ErrorCodes.PublishIncrementInvalid);
    }
}
