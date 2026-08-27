using Core.Validation.Extensions;
using E3A.Application.Exceptions;
using FluentValidation;

namespace E3A.Application.Engineers.GetEngineer;

public sealed class GetEngineerQueryValidator : AbstractValidator<GetEngineerQuery>
{
    public GetEngineerQueryValidator()
    {
        RuleFor(x => x.EngineerId).ValidateRequired(ErrorCodes.EngineerIdRequired);
    }
}
