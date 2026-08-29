using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using E3A.Application.Exceptions;
using E3A.Application.Options;
using E3A.Application.Publishing.Shared;
using E3A.Domain.Engineers;
using E3A.Domain.Publishing;
using E3A.Domain.Teams;
using MediatR;
using Microsoft.Extensions.Options;

namespace E3A.Application.Publishing.GetPublishStatus;

public sealed class GetPublishStatusQueryHandler(IItemVersionRepository itemVersionRepository, IEngineerRepository engineerRepository, ITeamRepository teamRepository, ICurrentUserService currentUserService, IOptions<PublishingOptions> publishingOptions) : IRequestHandler<GetPublishStatusQuery, PublishStatusResult>
{
    public async Task<PublishStatusResult> Handle(GetPublishStatusQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId == null || userId == Guid.Empty)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var version = await itemVersionRepository.GetByIdAsync(request.VersionId, cancellationToken, asNoTracking: true).ConfigureAwait(false);

        if (version == null)
        {
            throw new NotFoundCoreException(ErrorCodes.PublishVersionNotFound);
        }

        var ownerUserId = version.ItemType switch
        {
            ItemType.Team => await ResolveTeamOwnerAsync(version.ItemId, cancellationToken).ConfigureAwait(false),
            _ => await ResolveEngineerOwnerAsync(version.ItemId, cancellationToken).ConfigureAwait(false),
        };

        if (ownerUserId != userId.Value)
        {
            throw new ForbiddenCoreException(version.ItemType == ItemType.Team ? ErrorCodes.TeamNotOwned : ErrorCodes.EngineerNotOwned);
        }

        return PublishStatusResultGenerator.Generate(version, publishingOptions.Value);
    }

    private async Task<Guid> ResolveTeamOwnerAsync(Guid teamId, CancellationToken cancellationToken)
    {
        var team = await teamRepository.GetByIdAsync(teamId, cancellationToken, asNoTracking: true).ConfigureAwait(false);

        return team?.OwnerUserId ?? throw new NotFoundCoreException(ErrorCodes.TeamNotFound);
    }

    private async Task<Guid> ResolveEngineerOwnerAsync(Guid engineerId, CancellationToken cancellationToken)
    {
        var engineer = await engineerRepository.GetByIdAsync(engineerId, cancellationToken, asNoTracking: true).ConfigureAwait(false);

        return engineer?.OwnerUserId ?? throw new NotFoundCoreException(ErrorCodes.EngineerNotFound);
    }
}
