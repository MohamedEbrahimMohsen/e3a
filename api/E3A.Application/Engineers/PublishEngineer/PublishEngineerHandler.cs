using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using E3A.Application.Exceptions;
using E3A.Application.Options;
using E3A.Application.Publishing.Shared;
using E3A.Domain.Engineers;
using E3A.Domain.Publishing;
using MediatR;
using Microsoft.Extensions.Options;

namespace E3A.Application.Engineers.PublishEngineer;

public sealed class PublishEngineerHandler(IEngineerRepository engineerRepository, IItemVersionRepository itemVersionRepository, ICurrentUserService currentUserService, IOptions<PublishingOptions> publishingOptions) : IRequestHandler<PublishEngineerCommand, PublishStatusResult>
{
    public async Task<PublishStatusResult> Handle(PublishEngineerCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId == null || userId == Guid.Empty)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var engineer = await engineerRepository.GetByIdAsync(request.EngineerId, cancellationToken).ConfigureAwait(false);

        if (engineer == null)
        {
            throw new NotFoundCoreException(ErrorCodes.EngineerNotFound);
        }

        if (engineer.OwnerUserId != userId.Value)
        {
            throw new ForbiddenCoreException(ErrorCodes.EngineerNotOwned);
        }

        if (string.IsNullOrWhiteSpace(engineer.DraftManifestJson))
        {
            throw new BadRequestCoreException(ErrorCodes.EngineerDraftNotUploaded);
        }

        var inProgress = await itemVersionRepository.FirstOrDefaultAsync(x => x.ItemType == ItemType.Engineer && x.ItemId == engineer.Id && (x.Status == ItemVersionStatus.Queued || x.Status == ItemVersionStatus.Building), cancellationToken).ConfigureAwait(false);

        if (inProgress != null)
        {
            throw new ConflictCoreException(ErrorCodes.PublishAlreadyInProgress);
        }

        var options = publishingOptions.Value;
        var versionCount = await itemVersionRepository.CountAsync(cancellationToken, x => x.ItemType == ItemType.Engineer && x.ItemId == engineer.Id).ConfigureAwait(false);

        if (versionCount >= options.MaxVersionsPerItem)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.PublishVersionLimitReached, context: new Dictionary<string, object> { ["limit"] = options.MaxVersionsPerItem });
        }

        var latest = await itemVersionRepository.FirstOrDefaultAsync(x => x.ItemType == ItemType.Engineer && x.ItemId == engineer.Id, cancellationToken, orderBy: query => query.OrderByDescending(x => x.VersionNumber)).ConfigureAwait(false);
        var semanticVersion = SemanticVersionCalculator.Next(latest?.SemanticVersion, request.Increment);
        var version = ItemVersion.Create(ItemType.Engineer, engineer.Id, (latest?.VersionNumber ?? 0) + 1, semanticVersion, engineer.DraftManifestJson, userId.Value);

        await itemVersionRepository.AddAsync(version, cancellationToken).ConfigureAwait(false);
        await itemVersionRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return PublishStatusResultGenerator.Generate(version, options);
    }
}
