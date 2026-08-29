using System.Text.Json;
using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using E3A.Application.Exceptions;
using E3A.Application.Options;
using E3A.Application.Publishing.Shared;
using E3A.Application.Teams.Shared;
using E3A.Domain.Publishing;
using E3A.Domain.Teams;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace E3A.Application.Teams.PublishTeam;

public sealed class PublishTeamHandler(ITeamRepository teamRepository, IItemVersionRepository itemVersionRepository, ICurrentUserService currentUserService, IOptions<PublishingOptions> publishingOptions) : IRequestHandler<PublishTeamCommand, PublishStatusResult>
{
    public async Task<PublishStatusResult> Handle(PublishTeamCommand request, CancellationToken cancellationToken)
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

        if (team.Members.Count == 0)
        {
            throw new BadRequestCoreException(ErrorCodes.TeamEmpty);
        }

        var inProgress = await itemVersionRepository.FirstOrDefaultAsync(x => x.ItemType == ItemType.Team && x.ItemId == team.Id && (x.Status == ItemVersionStatus.Queued || x.Status == ItemVersionStatus.Building), cancellationToken).ConfigureAwait(false);

        if (inProgress != null)
        {
            throw new ConflictCoreException(ErrorCodes.PublishAlreadyInProgress);
        }

        var options = publishingOptions.Value;
        var versionCount = await itemVersionRepository.CountAsync(cancellationToken, x => x.ItemType == ItemType.Team && x.ItemId == team.Id).ConfigureAwait(false);

        if (versionCount >= options.MaxVersionsPerItem)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.PublishVersionLimitReached, context: new Dictionary<string, object> { ["limit"] = options.MaxVersionsPerItem });
        }

        var latest = await itemVersionRepository.FirstOrDefaultAsync(x => x.ItemType == ItemType.Team && x.ItemId == team.Id, cancellationToken, orderBy: query => query.OrderByDescending(x => x.VersionNumber)).ConfigureAwait(false);
        var semanticVersion = SemanticVersionCalculator.Next(latest?.SemanticVersion, request.Increment);
        var frozenRosterJson = JsonSerializer.Serialize(TeamRosterGenerator.Generate(team));
        var version = ItemVersion.Create(ItemType.Team, team.Id, (latest?.VersionNumber ?? 0) + 1, semanticVersion, frozenRosterJson, userId.Value);

        await itemVersionRepository.AddAsync(version, cancellationToken).ConfigureAwait(false);
        await itemVersionRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return PublishStatusResultGenerator.Generate(version, options);
    }
}
