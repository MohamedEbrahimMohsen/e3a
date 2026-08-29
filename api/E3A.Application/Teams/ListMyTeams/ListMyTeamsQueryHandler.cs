using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using E3A.Application.Exceptions;
using E3A.Application.Teams.Shared;
using E3A.Domain.Teams;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace E3A.Application.Teams.ListMyTeams;

public sealed class ListMyTeamsQueryHandler(ITeamRepository teamRepository, ICurrentUserService currentUserService) : IRequestHandler<ListMyTeamsQuery, List<TeamResult>>
{
    public async Task<List<TeamResult>> Handle(ListMyTeamsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId == null || userId == Guid.Empty)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var ownerUserId = userId.Value;
        var teams = await teamRepository.FindAsync(x => x.OwnerUserId == ownerUserId, cancellationToken, include: query => query.Include(x => x.Members), asNoTracking: true).ConfigureAwait(false);

        return teams
            .OrderByDescending(x => x.CreationDate)
            .Select(TeamResultGenerator.Generate)
            .ToList();
    }
}
