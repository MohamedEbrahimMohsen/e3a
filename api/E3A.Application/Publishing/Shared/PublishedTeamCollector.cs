using Core.Errors;
using E3A.Application.Exceptions;
using E3A.Application.Options;
using E3A.Domain.Identity;
using E3A.Domain.Publishing;
using E3A.Domain.Teams;

namespace E3A.Application.Publishing.Shared;

public static class PublishedTeamCollector
{
    public static async Task<List<MarketplacePlugin>> CollectAsync(ITeamRepository teamRepository, IItemVersionRepository itemVersionRepository, IUserRepository userRepository, PublishingOptions options, CancellationToken cancellationToken)
    {
        List<Team> published = [];
        var pageNumber = 1;
        var hasMorePages = true;

        while (hasMorePages)
        {
            var page = await teamRepository.FindPaginatedAsync(pageNumber, options.MarketplacePageSize, cancellationToken, x => x.Status == TeamStatus.Published && x.LatestVersionId != null, orderBy: query => query.OrderBy(x => x.Slug), asNoTracking: true).ConfigureAwait(false);
            published.AddRange(page.Items);
            hasMorePages = pageNumber < page.TotalPages;

            if (hasMorePages)
            {
                pageNumber++;

                if (pageNumber > options.MarketplaceMaxPages)
                {
                    throw new InternalServerErrorCoreException(ErrorCodes.MarketplaceTeamLimitExceeded);
                }
            }
        }

        var versionIds = published.Select(x => x.LatestVersionId!.Value).ToList();
        var versions = await itemVersionRepository.FindAsync(x => versionIds.Contains(x.Id) && x.Status == ItemVersionStatus.Published, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var ownerIds = published.Select(x => x.OwnerUserId).Distinct().ToList();
        var users = await userRepository.FindAsync(x => ownerIds.Contains(x.Id), cancellationToken, asNoTracking: true).ConfigureAwait(false);

        return published
            .Select(team => new PublishedTeamVersion(team, versions.Find(x => x.Id == team.LatestVersionId!.Value)))
            .Where(x => x.Version != null)
            .Select(x => MarketplaceDocumentGenerator.GeneratePlugin(x.Team, x.Version!, ResolveAuthorName(x.Team, users), options))
            .ToList();
    }

    private static string ResolveAuthorName(Team team, List<User> users)
    {
        var user = users.Find(x => x.Id == team.OwnerUserId);
        return string.IsNullOrWhiteSpace(user?.UserName) ? team.Slug : user.UserName;
    }

    private sealed record PublishedTeamVersion(Team Team, ItemVersion? Version);
}
