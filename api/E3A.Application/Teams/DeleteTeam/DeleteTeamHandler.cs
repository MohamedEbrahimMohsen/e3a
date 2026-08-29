using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using E3A.Application.Exceptions;
using E3A.Domain.Teams;
using MediatR;

namespace E3A.Application.Teams.DeleteTeam;

public sealed class DeleteTeamHandler(ITeamRepository teamRepository, ICurrentUserService currentUserService) : IRequestHandler<DeleteTeamCommand>
{
    public async Task Handle(DeleteTeamCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId == null || userId == Guid.Empty)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var team = await teamRepository.GetByIdAsync(request.TeamId, cancellationToken).ConfigureAwait(false);

        if (team == null)
        {
            throw new NotFoundCoreException(ErrorCodes.TeamNotFound);
        }

        if (team.OwnerUserId != userId.Value)
        {
            throw new ForbiddenCoreException(ErrorCodes.TeamNotOwned);
        }

        team.Delete();

        teamRepository.Update(team);
        await teamRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
