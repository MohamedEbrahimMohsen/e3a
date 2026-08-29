using Core.Validation.Extensions;
using E3A.Application.Exceptions;
using E3A.Application.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace E3A.Application.Teams.SetTeamMembers;

public sealed class SetTeamMembersValidator : AbstractValidator<SetTeamMembersCommand>
{
    public SetTeamMembersValidator(IOptions<TeamsOptions> teamsOptions)
    {
        var options = teamsOptions.Value;

        RuleFor(x => x.TeamId).ValidateRequired(ErrorCodes.TeamIdRequired);

        RuleFor(x => x.Members)
            .ValidateListMaxItems(options.MaxMembersPerTeam, ErrorCodes.TeamMemberLimitReached);

        RuleFor(x => x.Members)
            .Must(members => members.Select(x => x.EngineerId).Distinct().Count() == members.Count)
            .WithMessage("{PropertyName} must not repeat an engineer.")
            .WithErrorCode(ErrorCodes.TeamMemberDuplicate);

        RuleForEach(x => x.Members)
            .ChildRules(member => member.RuleFor(x => x.EngineerId).ValidateRequired(ErrorCodes.TeamMemberEngineerIdRequired));
    }
}
