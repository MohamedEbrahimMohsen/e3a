using System.Text;
using System.Text.Json;
using Core.Azure.Clients;
using Core.Errors;
using E3A.Application.Engineers.Shared;
using E3A.Application.Exceptions;
using E3A.Application.Options;
using E3A.Application.Publishing.Security;
using E3A.Application.Publishing.Shared;
using E3A.Domain.Engineers;
using E3A.Domain.Identity;
using E3A.Domain.Publishing;
using MediatR;
using Microsoft.Extensions.Options;

namespace E3A.Application.Publishing.ProcessPublishJob;

public sealed class ProcessPublishJobHandler(IItemVersionRepository itemVersionRepository, IEngineerRepository engineerRepository, IUserRepository userRepository, IStorageBlobClient storageBlobClient, IOptions<AzureOptions> azureOptions, IOptions<PublishingOptions> publishingOptions, IOptions<UploadsOptions> uploadsOptions) : IRequestHandler<ProcessPublishJobCommand>
{
    public async Task Handle(ProcessPublishJobCommand request, CancellationToken cancellationToken)
    {
        var version = await itemVersionRepository.GetByIdAsync(request.VersionId, cancellationToken).ConfigureAwait(false);

        if (version == null)
        {
            throw new NotFoundCoreException(ErrorCodes.PublishVersionNotFound);
        }

        if (version.Status is not (ItemVersionStatus.Queued or ItemVersionStatus.Building))
        {
            return;
        }

        var engineer = await engineerRepository.GetByIdAsync(version.ItemId, cancellationToken).ConfigureAwait(false);

        if (engineer == null)
        {
            await FailAsync(version, ErrorCodes.EngineerNotFound, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (version.Status == ItemVersionStatus.Queued)
        {
            version.MarkBuilding();
            itemVersionRepository.Update(version);
            await itemVersionRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var azure = azureOptions.Value;
        var publishing = publishingOptions.Value;
        var snapshotAssets = await DraftSnapshotFreezer.FreezeAsync(storageBlobClient, azure, engineer.OwnerUserId, engineer.Id, version.Id, cancellationToken).ConfigureAwait(false);

        if (snapshotAssets.Count == 0)
        {
            await FailAsync(version, ErrorCodes.EngineerSnapshotEmpty, cancellationToken).ConfigureAwait(false);
            return;
        }

        var manifest = JsonSerializer.Deserialize<ImportManifestResult>(version.FrozenManifestJson);

        if (manifest == null)
        {
            await FailAsync(version, ErrorCodes.EngineerDraftNotUploaded, cancellationToken).ConfigureAwait(false);
            return;
        }

        var user = await userRepository.GetByIdAsync(engineer.OwnerUserId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var authorName = string.IsNullOrWhiteSpace(user?.UserName) ? engineer.Slug : user.UserName;
        var pluginFiles = PluginTreeAssembler.Assemble(snapshotAssets, manifest, engineer, version.SemanticVersion, authorName, publishing);
        var errors = PluginStructureValidator.Validate(pluginFiles, manifest, publishing);

        if (errors.Count > 0)
        {
            await FailAsync(version, string.Join(", ", errors), cancellationToken).ConfigureAwait(false);
            return;
        }

        var scanReport = SecurityScanner.Scan(pluginFiles, uploadsOptions.Value.HookScriptExtensions, publishing);
        version.RecordScanReport(ScanReportSerializer.Serialize(scanReport, publishing));

        if (scanReport.IsBlocked)
        {
            await RejectAsync(version, ErrorCodes.PluginSecurityScanBlocked, cancellationToken).ConfigureAwait(false);
            return;
        }

        var zipped = DeterministicZipper.Create(pluginFiles);
        var pluginName = PluginName.For(engineer.Slug);
        var zipBlobPath = PublishBlobPaths.Zip(pluginName, version.SemanticVersion);
        var existingZips = await storageBlobClient.ListByPrefixAsync(azure.ManagedIdentityClientId, azure.StorageAccountUrl, azure.PublicBlobContainerName, zipBlobPath, cancellationToken).ConfigureAwait(false);

        if (!existingZips.Exists(name => string.Equals(name, zipBlobPath, StringComparison.Ordinal)))
        {
            using var zipStream = new MemoryStream(zipped.Content);
            await storageBlobClient.UploadAsync(zipStream, azure.ManagedIdentityClientId, azure.StorageAccountUrl, azure.PublicBlobContainerName, zipBlobPath, PublishBlobPaths.ZipContentType, publishing.ZipCacheControl, overwrite: false, cancellationToken).ConfigureAwait(false);
        }

        version.MarkPublished(zipBlobPath, zipped.Sha256, zipped.SizeBytes);
        engineer.MarkPublished(version.Id);

        var pinnedJson = MarketplaceDocumentGenerator.Generate([MarketplaceDocumentGenerator.GeneratePlugin(engineer, version, authorName, publishing)], publishing);

        using var pinnedStream = new MemoryStream(Encoding.UTF8.GetBytes(pinnedJson));
        await storageBlobClient.UploadAsync(pinnedStream, azure.ManagedIdentityClientId, azure.StorageAccountUrl, azure.PublicBlobContainerName, PublishBlobPaths.PinnedMarketplace(pluginName, version.SemanticVersion), PublishBlobPaths.MarketplaceContentType, publishing.MarketplaceCacheControl, overwrite: true, cancellationToken).ConfigureAwait(false);

        itemVersionRepository.Update(version);
        engineerRepository.Update(engineer);
        await itemVersionRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RejectAsync(ItemVersion version, string failureReason, CancellationToken cancellationToken)
    {
        version.MarkRejected(failureReason);
        itemVersionRepository.Update(version);
        await itemVersionRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task FailAsync(ItemVersion version, string failureReason, CancellationToken cancellationToken)
    {
        version.MarkFailed(failureReason);
        itemVersionRepository.Update(version);
        await itemVersionRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
