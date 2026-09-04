using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using E3A.Application.Exceptions;
using E3A.Application.Teams.Shared;
using E3A.Domain.Teams;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace E3A.Application.Teams.GetTeam;

public sealed class GetTeamQueryHandler(ITeamRepository teamRepository, ICurrentUserService currentUserService) : IRequestHandler<GetTeamQuery, TeamDetailResult>
{
    public async Task<TeamDetailResult> Handle(GetTeamQuery request, CancellationToken cancellationToken)
    {
        var team = await teamRepository.GetByIdAsync(request.TeamId, cancellationToken, include: query => query.Include(x => x.Members), asNoTracking: true).ConfigureAwait(false);

        if (team == null)
        {
            throw new NotFoundCoreException(ErrorCodes.TeamNotFound);
        }

        if (team.Status == TeamStatus.Published)
        {
            return TeamResultGenerator.GenerateDetail(team);
        }

        var userId = currentUserService.UserId;

        if (userId == null || userId == Guid.Empty)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        if (team.OwnerUserId != userId.Value)
        {
            throw new ForbiddenCoreException(ErrorCodes.TeamNotOwned);
        }

        return TeamResultGenerator.GenerateDetail(team);
    }
}
