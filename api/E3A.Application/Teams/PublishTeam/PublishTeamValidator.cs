using Core.Validation.Extensions;
using E3A.Application.Exceptions;
using FluentValidation;

namespace E3A.Application.Teams.PublishTeam;

public sealed class PublishTeamValidator : AbstractValidator<PublishTeamCommand>
{
    public PublishTeamValidator()
    {
        RuleFor(x => x.TeamId).ValidateRequired(ErrorCodes.TeamIdRequired);
        RuleFor(x => x.Increment).IsInEnum().WithErrorCode(ErrorCodes.PublishIncrementInvalid);
    }
}
