using System.Text.Json;
using Core.Azure.Clients;
using E3A.Application.Engineers.Shared;
using E3A.Application.Exceptions;
using E3A.Application.Options;
using E3A.Domain.Engineers;
using E3A.Domain.Identity;
using E3A.Domain.Publishing;

namespace E3A.Application.Publishing.Shared;

public static class EngineerPublishBuilder
{
    public static async Task<PublishBuild> BuildAsync(IEngineerRepository engineerRepository, IUserRepository userRepository, IStorageBlobClient storageBlobClient, AzureOptions azureOptions, PublishingOptions publishingOptions, ItemVersion version, CancellationToken cancellationToken)
    {
        var engineer = await engineerRepository.GetByIdAsync(version.ItemId, cancellationToken).ConfigureAwait(false);

        if (engineer == null)
        {
            return Failed(ErrorCodes.EngineerNotFound);
        }

        var snapshotAssets = await DraftSnapshotFreezer.FreezeAsync(storageBlobClient, azureOptions, engineer.OwnerUserId, engineer.Id, version.Id, cancellationToken).ConfigureAwait(false);

        if (snapshotAssets.Count == 0)
        {
            return Failed(ErrorCodes.EngineerSnapshotEmpty);
        }

        var manifest = JsonSerializer.Deserialize<ImportManifestResult>(version.FrozenManifestJson);

        if (manifest == null)
        {
            return Failed(ErrorCodes.EngineerDraftNotUploaded);
        }

        var user = await userRepository.GetByIdAsync(engineer.OwnerUserId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var authorName = string.IsNullOrWhiteSpace(user?.UserName) ? engineer.Slug : user.UserName;
        var files = PluginTreeAssembler.Assemble(snapshotAssets, manifest, engineer, version.SemanticVersion, authorName, publishingOptions);
        var errors = PluginStructureValidator.Validate(files, manifest, publishingOptions);

        return errors.Count > 0
            ? Failed(string.Join(", ", errors))
            : new PublishBuild(engineer, null, PluginName.ForEngineer(engineer.Slug), authorName, files, null);
    }

    private static PublishBuild Failed(string failureReason)
    {
        return new PublishBuild(null, null, string.Empty, string.Empty, [], failureReason);
    }
}
