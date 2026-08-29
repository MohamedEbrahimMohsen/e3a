using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using E3A.Application.Exceptions;
using E3A.Application.Teams.Shared;
using E3A.Domain.Engineers;
using E3A.Domain.Publishing;
using E3A.Domain.Teams;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace E3A.Application.Teams.SetTeamMembers;

public sealed class SetTeamMembersHandler(ITeamRepository teamRepository, IEngineerRepository engineerRepository, IItemVersionRepository itemVersionRepository, ICurrentUserService currentUserService) : IRequestHandler<SetTeamMembersCommand, TeamDetailResult>
{
    public async Task<TeamDetailResult> Handle(SetTeamMembersCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId == null || userId == Guid.Empty)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var team = await teamRepository.GetByIdAsync(request.TeamId, cancellationToken, include: query => query.Include(x => x.Members)).ConfigureAwait(false);

        if (team == null)
        {
            throw new NotFoundCoreException(ErrorCodes.TeamNotFound);
        }

        if (team.OwnerUserId != userId.Value)
        {
            throw new ForbiddenCoreException(ErrorCodes.TeamNotOwned);
        }

        team.ReplaceMembers(await ResolvePinsAsync(request, team, cancellationToken).ConfigureAwait(false), userId.Value);

        teamRepository.Update(team);
        await teamRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return TeamResultGenerator.GenerateDetail(team);
    }

    private async Task<List<TeamMemberPin>> ResolvePinsAsync(SetTeamMembersCommand request, Team team, CancellationToken cancellationToken)
    {
        if (request.Members.Count == 0)
        {
            return [];
        }

        var engineerIds = request.Members.Select(x => x.EngineerId).ToList();
        var engineers = await engineerRepository.FindAsync(x => engineerIds.Contains(x.Id), cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var versionIds = TeamMemberPinResolver.ResolveVersionIds(request.Members, engineers, team.Members);
        var versions = await itemVersionRepository.FindAsync(x => versionIds.Contains(x.Id), cancellationToken, asNoTracking: true).ConfigureAwait(false);

        return TeamMemberPinResolver.ResolvePins(request.Members, engineers, versions, team.Members);
    }
}
