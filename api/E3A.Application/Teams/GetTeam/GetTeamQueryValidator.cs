using Core.Validation.Extensions;
using E3A.Application.Exceptions;
using FluentValidation;

namespace E3A.Application.Teams.GetTeam;

public sealed class GetTeamQueryValidator : AbstractValidator<GetTeamQuery>
{
    public GetTeamQueryValidator()
    {
        RuleFor(x => x.TeamId).ValidateRequired(ErrorCodes.TeamIdRequired);
    }
}
