using Core.Errors;
using E3A.Application.Exceptions;
using E3A.Application.Options;
using E3A.Domain.Engineers;
using E3A.Domain.Identity;
using E3A.Domain.Publishing;

namespace E3A.Application.Publishing.Shared;

public static class PublishedEngineerCollector
{
    public static async Task<List<MarketplacePlugin>> CollectAsync(IEngineerRepository engineerRepository, IItemVersionRepository itemVersionRepository, IUserRepository userRepository, PublishingOptions options, CancellationToken cancellationToken)
    {
        List<Engineer> published = [];
        var pageNumber = 1;
        var hasMorePages = true;

        while (hasMorePages)
        {
            var page = await engineerRepository.FindPaginatedAsync(pageNumber, options.MarketplacePageSize, cancellationToken, x => x.Status == EngineerStatus.Published && x.LatestVersionId != null, orderBy: query => query.OrderBy(x => x.Slug), asNoTracking: true).ConfigureAwait(false);
            published.AddRange(page.Items);
            hasMorePages = pageNumber < page.TotalPages;

            if (hasMorePages)
            {
                pageNumber++;

                if (pageNumber > options.MarketplaceMaxPages)
                {
                    throw new InternalServerErrorCoreException(ErrorCodes.MarketplaceEngineerLimitExceeded);
                }
            }
        }

        var versionIds = published.Select(x => x.LatestVersionId!.Value).ToList();
        var versions = await itemVersionRepository.FindAsync(x => versionIds.Contains(x.Id) && x.Status == ItemVersionStatus.Published, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var ownerIds = published.Select(x => x.OwnerUserId).Distinct().ToList();
        var users = await userRepository.FindAsync(x => ownerIds.Contains(x.Id), cancellationToken, asNoTracking: true).ConfigureAwait(false);

        return published
            .Select(engineer => new PublishedEngineerVersion(engineer, versions.Find(x => x.Id == engineer.LatestVersionId!.Value)))
            .Where(x => x.Version != null)
            .Select(x => MarketplaceDocumentGenerator.GeneratePlugin(x.Engineer, x.Version!, ResolveAuthorName(x.Engineer, users), options))
            .ToList();
    }

    private static string ResolveAuthorName(Engineer engineer, List<User> users)
    {
        var user = users.Find(x => x.Id == engineer.OwnerUserId);
        return string.IsNullOrWhiteSpace(user?.UserName) ? engineer.Slug : user.UserName;
    }

    private sealed record PublishedEngineerVersion(Engineer Engineer, ItemVersion? Version);
}
