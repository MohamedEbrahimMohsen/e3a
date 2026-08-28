using E3A.Application.Catalog.Shared;
using E3A.Domain.Engineers;
using MediatR;

namespace E3A.Application.Catalog.GetCatalogTags;

public sealed class GetCatalogTagsQueryHandler(IEngineerRepository engineerRepository) : IRequestHandler<GetCatalogTagsQuery, List<CatalogTagResult>>
{
    public async Task<List<CatalogTagResult>> Handle(GetCatalogTagsQuery request, CancellationToken cancellationToken)
    {
        var engineers = await engineerRepository.FindAsync(x => x.Status == EngineerStatus.Published, cancellationToken, asNoTracking: true).ConfigureAwait(false);

        return engineers
            .SelectMany(x => x.Tags.Select(tag => tag.ToLowerInvariant()).Distinct())
            .GroupBy(tag => tag)
            .Select(group => new CatalogTagResult(group.Key, group.Count()))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Tag, StringComparer.Ordinal)
            .ToList();
    }
}
