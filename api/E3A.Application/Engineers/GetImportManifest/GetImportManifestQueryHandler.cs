using System.Text.Json;
using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using E3A.Application.Engineers.Shared;
using E3A.Application.Exceptions;
using E3A.Domain.Engineers;
using MediatR;

namespace E3A.Application.Engineers.GetImportManifest;

public sealed class GetImportManifestQueryHandler(IEngineerRepository engineerRepository, ICurrentUserService currentUserService) : IRequestHandler<GetImportManifestQuery, ImportManifestResult>
{
    public async Task<ImportManifestResult> Handle(GetImportManifestQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId == null || userId == Guid.Empty)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var ownerUserId = userId.Value;
        var engineer = await engineerRepository.GetByIdAsync(request.EngineerId, cancellationToken, asNoTracking: true).ConfigureAwait(false);

        if (engineer == null)
        {
            throw new NotFoundCoreException(ErrorCodes.EngineerNotFound);
        }

        if (engineer.OwnerUserId != ownerUserId)
        {
            throw new ForbiddenCoreException(ErrorCodes.EngineerNotOwned);
        }

        if (engineer.DraftManifestJson == null)
        {
            throw new NotFoundCoreException(ErrorCodes.EngineerDraftNotUploaded);
        }

        return JsonSerializer.Deserialize<ImportManifestResult>(engineer.DraftManifestJson)!;
    }
}
