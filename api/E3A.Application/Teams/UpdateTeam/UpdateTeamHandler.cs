using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Utilities.Generator;
using E3A.Application.Exceptions;
using E3A.Application.Options;
using E3A.Application.Shared;
using E3A.Application.Teams.Shared;
using E3A.Domain.SharedKernel;
using E3A.Domain.Teams;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace E3A.Application.Teams.UpdateTeam;

public sealed class UpdateTeamHandler(ITeamRepository teamRepository, ICurrentUserService currentUserService, IGenerator generator, IOptions<TeamsOptions> teamsOptions) : IRequestHandler<UpdateTeamCommand, TeamResult>
{
    public async Task<TeamResult> Handle(UpdateTeamCommand request, CancellationToken cancellationToken)
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

        var resolvedSlug = await ResolveSlugChangeAsync(request, team, cancellationToken).ConfigureAwait(false);

        team.UpdateMetadata(request.DisplayName, request.Description, request.Tags);

        if (resolvedSlug != null)
        {
            team.ChangeSlug(resolvedSlug);
        }

        teamRepository.Update(team);
        await teamRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return TeamResultGenerator.Generate(team);
    }

    private async Task<string?> ResolveSlugChangeAsync(UpdateTeamCommand request, Team team, CancellationToken cancellationToken)
    {
        if (request.Slug == null)
        {
            return null;
        }

        var requestedSlug = SlugGenerator.NormalizeTypedSlug(request.Slug);

        if (requestedSlug == team.Slug)
        {
            return null;
        }

        if (!team.IsSlugMutable)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.TeamSlugFrozen);
        }

        return await SlugResolver.ResolveUniqueAsync(requestedSlug, teamRepository.IsSlugExistsAsync, generator, teamsOptions.Value.SlugMaxLength, teamsOptions.Value.SlugSuffixSize, cancellationToken).ConfigureAwait(false);
    }
}
