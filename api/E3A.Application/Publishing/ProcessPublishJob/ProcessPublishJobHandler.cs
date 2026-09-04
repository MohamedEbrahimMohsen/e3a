using System.Text;
using Core.Azure.Clients;
using Core.Errors;
using E3A.Application.Exceptions;
using E3A.Application.Options;
using E3A.Application.Publishing.Security;
using E3A.Application.Publishing.Shared;
using E3A.Domain.Engineers;
using E3A.Domain.Identity;
using E3A.Domain.Publishing;
using E3A.Domain.Teams;
using MediatR;
using Microsoft.Extensions.Options;

namespace E3A.Application.Publishing.ProcessPublishJob;

public sealed class ProcessPublishJobHandler(IItemVersionRepository itemVersionRepository, IEngineerRepository engineerRepository, ITeamRepository teamRepository, IUserRepository userRepository, IStorageBlobClient storageBlobClient, IOptions<AzureOptions> azureOptions, IOptions<PublishingOptions> publishingOptions, IOptions<UploadsOptions> uploadsOptions) : IRequestHandler<ProcessPublishJobCommand>
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

        if (version.Status == ItemVersionStatus.Queued)
        {
            version.MarkBuilding();
            itemVersionRepository.Update(version);
            await itemVersionRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var azure = azureOptions.Value;
        var publishing = publishingOptions.Value;
        var build = version.ItemType switch
        {
            ItemType.Team => await TeamPublishBuilder.BuildAsync(teamRepository, itemVersionRepository, userRepository, storageBlobClient, azure, publishing, version, cancellationToken).ConfigureAwait(false),
            _ => await EngineerPublishBuilder.BuildAsync(engineerRepository, userRepository, storageBlobClient, azure, publishing, version, cancellationToken).ConfigureAwait(false),
        };

        if (build.FailureReason != null)
        {
            await FailAsync(version, build.FailureReason, cancellationToken).ConfigureAwait(false);
            return;
        }

        var scanReport = SecurityScanner.Scan(build.Files, uploadsOptions.Value.HookScriptExtensions, publishing);
        version.RecordScanReport(ScanReportSerializer.Serialize(scanReport, publishing));

        if (scanReport.IsBlocked)
        {
            await RejectAsync(version, ErrorCodes.PluginSecurityScanBlocked, cancellationToken).ConfigureAwait(false);
            return;
        }

        var zipped = DeterministicZipper.Create(build.Files);
        var zipBlobPath = PublishBlobPaths.Zip(build.PluginName, version.SemanticVersion);
        var existingZips = await storageBlobClient.ListByPrefixAsync(azure.ManagedIdentityClientId, azure.StorageAccountUrl, azure.PublicBlobContainerName, zipBlobPath, cancellationToken).ConfigureAwait(false);

        if (!existingZips.Exists(name => string.Equals(name, zipBlobPath, StringComparison.Ordinal)))
        {
            using var zipStream = new MemoryStream(zipped.Content);
            await storageBlobClient.UploadAsync(zipStream, azure.ManagedIdentityClientId, azure.StorageAccountUrl, azure.PublicBlobContainerName, zipBlobPath, PublishBlobPaths.ZipContentType, publishing.ZipCacheControl, overwrite: false, cancellationToken).ConfigureAwait(false);
        }

        version.MarkPublished(zipBlobPath, zipped.Sha256, zipped.SizeBytes);
        MarkItemPublished(build, version.Id);

        var pinnedJson = MarketplaceDocumentGenerator.Generate([GeneratePinnedPlugin(build, version, publishing)], publishing);

        using var pinnedStream = new MemoryStream(Encoding.UTF8.GetBytes(pinnedJson));
        await storageBlobClient.UploadAsync(pinnedStream, azure.ManagedIdentityClientId, azure.StorageAccountUrl, azure.PublicBlobContainerName, PublishBlobPaths.PinnedMarketplace(build.PluginName, version.SemanticVersion), PublishBlobPaths.MarketplaceContentType, publishing.MarketplaceCacheControl, overwrite: true, cancellationToken).ConfigureAwait(false);

        itemVersionRepository.Update(version);
        await itemVersionRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RejectAsync(ItemVersion version, string failureReason, CancellationToken cancellationToken)
    {
        version.MarkRejected(failureReason);
        itemVersionRepository.Update(version);
        await itemVersionRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private void MarkItemPublished(PublishBuild build, Guid versionId)
    {
        if (build.Team != null)
        {
            build.Team.MarkPublished(versionId);
            teamRepository.Update(build.Team);
        }
        else
        {
            build.Engineer!.MarkPublished(versionId);
            engineerRepository.Update(build.Engineer);
        }
    }

    private static MarketplacePlugin GeneratePinnedPlugin(PublishBuild build, ItemVersion version, PublishingOptions options)
        => build.Team != null
            ? MarketplaceDocumentGenerator.GeneratePlugin(build.Team, version, build.AuthorName, options)
            : MarketplaceDocumentGenerator.GeneratePlugin(build.Engineer!, version, build.AuthorName, options);

    private async Task FailAsync(ItemVersion version, string failureReason, CancellationToken cancellationToken)
    {
        version.MarkFailed(failureReason);
        itemVersionRepository.Update(version);
        await itemVersionRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
