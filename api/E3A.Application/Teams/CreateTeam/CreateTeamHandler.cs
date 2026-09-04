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
using Microsoft.Extensions.Options;

namespace E3A.Application.Teams.CreateTeam;

public sealed class CreateTeamHandler(ITeamRepository teamRepository, ICurrentUserService currentUserService, IGenerator generator, IOptions<TeamsOptions> teamsOptions) : IRequestHandler<CreateTeamCommand, TeamResult>
{
    public async Task<TeamResult> Handle(CreateTeamCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId == null || userId == Guid.Empty)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var ownerUserId = userId.Value;
        var options = teamsOptions.Value;
        var ownedTeamCount = await teamRepository.CountAsync(cancellationToken, x => x.OwnerUserId == ownerUserId).ConfigureAwait(false);

        if (ownedTeamCount >= options.MaxTeamsPerCreator)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.TeamLimitReached, context: new Dictionary<string, object> { ["limit"] = options.MaxTeamsPerCreator });
        }

        var slug = await SlugResolver.ResolveUniqueAsync(SlugGenerator.NormalizeTypedSlug(request.Slug), teamRepository.IsSlugExistsAsync, generator, options.SlugMaxLength, options.SlugSuffixSize, cancellationToken).ConfigureAwait(false);
        var team = Team.Create(ownerUserId, slug, request.DisplayName, request.Description, request.Tags);

        await teamRepository.AddAsync(team, cancellationToken).ConfigureAwait(false);
        await teamRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return TeamResultGenerator.Generate(team);
    }
}
