using Core.Validation.Extensions;
using E3A.Application.Exceptions;
using FluentValidation;

namespace E3A.Application.Teams.DeleteTeam;

public sealed class DeleteTeamValidator : AbstractValidator<DeleteTeamCommand>
{
    public DeleteTeamValidator()
    {
        RuleFor(x => x.TeamId).ValidateRequired(ErrorCodes.TeamIdRequired);
    }
}
