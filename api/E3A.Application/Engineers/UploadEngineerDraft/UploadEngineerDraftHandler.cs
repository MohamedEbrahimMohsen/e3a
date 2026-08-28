using System.Text.Json;
using Core.Azure.Clients;
using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using E3A.Application.Engineers.Shared;
using E3A.Application.Exceptions;
using E3A.Application.Options;
using E3A.Domain.Engineers;
using E3A.Domain.Publishing;
using MediatR;
using Microsoft.Extensions.Options;

namespace E3A.Application.Engineers.UploadEngineerDraft;

public sealed class UploadEngineerDraftHandler(IEngineerRepository engineerRepository, IItemVersionRepository itemVersionRepository, ICurrentUserService currentUserService, IStorageBlobClient storageBlobClient, IOptions<UploadsOptions> uploadsOptions, IOptions<AzureOptions> azureOptions) : IRequestHandler<UploadEngineerDraftCommand, ImportManifestResult>
{
    public async Task<ImportManifestResult> Handle(UploadEngineerDraftCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId == null || userId == Guid.Empty)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var ownerUserId = userId.Value;
        var engineer = await engineerRepository.GetByIdAsync(request.EngineerId, cancellationToken).ConfigureAwait(false);

        if (engineer == null)
        {
            throw new NotFoundCoreException(ErrorCodes.EngineerNotFound);
        }

        if (engineer.OwnerUserId != ownerUserId)
        {
            throw new ForbiddenCoreException(ErrorCodes.EngineerNotOwned);
        }

        var inProgress = await itemVersionRepository.FirstOrDefaultAsync(x => x.ItemType == ItemType.Engineer && x.ItemId == engineer.Id && (x.Status == ItemVersionStatus.Queued || x.Status == ItemVersionStatus.Building), cancellationToken).ConfigureAwait(false);

        if (inProgress != null)
        {
            throw new ConflictCoreException(ErrorCodes.PublishAlreadyInProgress);
        }

        var options = uploadsOptions.Value;
        var azure = azureOptions.Value;

        await using var zipStream = request.File.OpenReadStream();
        var files = ClaudeFolderZipReader.Read(zipStream, options);
        var sanitized = ClaudeFolderSanitizer.Sanitize(files, options);
        var normalizedPaths = UploadPathNormalizer.Normalize(sanitized.Files, options);
        var draft = DraftNormalizer.Normalize(normalizedPaths, sanitized.StrippedPaths, options, DateTimeOffset.UtcNow);
        var blobPrefix = $"{engineer.OwnerUserId}/{engineer.Id}/";

        await storageBlobClient.DeleteByPrefixAsync(azure.ManagedIdentityClientId, azure.StorageAccountUrl, azure.DraftsBlobContainerName, blobPrefix, cancellationToken).ConfigureAwait(false);

        foreach (var asset in draft.Assets)
        {
            using var contentStream = new MemoryStream(asset.Content);
            await storageBlobClient.UploadAsync(contentStream, azure.ManagedIdentityClientId, azure.StorageAccountUrl, azure.DraftsBlobContainerName, blobPrefix + asset.Path, cancellationToken).ConfigureAwait(false);
        }

        engineer.ReplaceDraftManifest(JsonSerializer.Serialize(draft.Manifest));

        engineerRepository.Update(engineer);
        await engineerRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return draft.Manifest;
    }
}
