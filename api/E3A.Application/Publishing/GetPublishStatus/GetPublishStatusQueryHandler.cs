using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using E3A.Application.Exceptions;
using E3A.Application.Options;
using E3A.Application.Publishing.Shared;
using E3A.Domain.Engineers;
using E3A.Domain.Publishing;
using MediatR;
using Microsoft.Extensions.Options;

namespace E3A.Application.Publishing.GetPublishStatus;

public sealed class GetPublishStatusQueryHandler(IItemVersionRepository itemVersionRepository, IEngineerRepository engineerRepository, ICurrentUserService currentUserService, IOptions<PublishingOptions> publishingOptions) : IRequestHandler<GetPublishStatusQuery, PublishStatusResult>
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

        var engineer = await engineerRepository.GetByIdAsync(version.ItemId, cancellationToken, asNoTracking: true).ConfigureAwait(false);

        if (engineer == null)
        {
            throw new NotFoundCoreException(ErrorCodes.EngineerNotFound);
        }

        if (engineer.OwnerUserId != userId.Value)
        {
            throw new ForbiddenCoreException(ErrorCodes.EngineerNotOwned);
        }

        return PublishStatusResultGenerator.Generate(version, publishingOptions.Value);
    }
}
