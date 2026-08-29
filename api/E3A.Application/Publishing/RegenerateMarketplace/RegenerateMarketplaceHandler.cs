using System.Text;
using Core.Azure.Clients;
using E3A.Application.Options;
using E3A.Application.Publishing.Shared;
using E3A.Domain.Engineers;
using E3A.Domain.Identity;
using E3A.Domain.Publishing;
using E3A.Domain.Teams;
using MediatR;
using Microsoft.Extensions.Options;

namespace E3A.Application.Publishing.RegenerateMarketplace;

public sealed class RegenerateMarketplaceHandler(IEngineerRepository engineerRepository, ITeamRepository teamRepository, IItemVersionRepository itemVersionRepository, IUserRepository userRepository, IStorageBlobClient storageBlobClient, IOptions<AzureOptions> azureOptions, IOptions<PublishingOptions> publishingOptions) : IRequestHandler<RegenerateMarketplaceCommand>
{
    public async Task Handle(RegenerateMarketplaceCommand request, CancellationToken cancellationToken)
    {
        var azure = azureOptions.Value;
        var publishing = publishingOptions.Value;
        var engineerPlugins = await PublishedEngineerCollector.CollectAsync(engineerRepository, itemVersionRepository, userRepository, publishing, cancellationToken).ConfigureAwait(false);
        var teamPlugins = await PublishedTeamCollector.CollectAsync(teamRepository, itemVersionRepository, userRepository, publishing, cancellationToken).ConfigureAwait(false);

        var plugins = engineerPlugins
            .Concat(teamPlugins)
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ToList();

        var json = MarketplaceDocumentGenerator.Generate(plugins, publishing);

        using var documentStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        await storageBlobClient.UploadAsync(documentStream, azure.ManagedIdentityClientId, azure.StorageAccountUrl, azure.PublicBlobContainerName, PublishBlobPaths.RootMarketplaceBlobName, PublishBlobPaths.MarketplaceContentType, publishing.MarketplaceCacheControl, overwrite: true, cancellationToken).ConfigureAwait(false);
    }
}
