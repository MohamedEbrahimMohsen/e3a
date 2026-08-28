using Core.DDD.Models;
using E3A.Application.Catalog.Shared;
using E3A.Application.Options;
using E3A.Domain.Engineers;
using MediatR;
using Microsoft.Extensions.Options;

namespace E3A.Application.Catalog.GetCatalog;

public sealed class GetCatalogQueryHandler(IEngineerRepository engineerRepository, IOptions<CatalogOptions> catalogOptions) : IRequestHandler<GetCatalogQuery, PageData<CatalogEngineerResult>>
{
    public async Task<PageData<CatalogEngineerResult>> Handle(GetCatalogQuery request, CancellationToken cancellationToken)
    {
        var options = catalogOptions.Value;
        var engineers = await engineerRepository.FindAsync(x => x.Status == EngineerStatus.Published, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var searchText = request.SearchText?.Trim();
        IEnumerable<Engineer> filtered = engineers;

        if (!string.IsNullOrEmpty(searchText))
        {
            filtered = filtered.Where(x => MatchesSearchText(x, searchText));
        }

        if (request.Tags.Count > 0)
        {
            filtered = filtered.Where(x => MatchesAnyTag(x, request.Tags));
        }

        var ordered = request.Sort switch
        {
            CatalogSort.Newest => filtered.OrderByDescending(x => x.CreationDate),
            _ => filtered.OrderByDescending(x => x.InstallCount).ThenByDescending(x => x.CreationDate),
        };

        var matched = ordered.ToList();
        var pageSize = request.PageSize ?? options.DefaultPageSize;

        var items = matched
            .Skip((request.PageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(CatalogEngineerResultGenerator.Generate)
            .ToList();

        return new PageData<CatalogEngineerResult>
        {
            Items = items,
            PageNumber = request.PageNumber,
            PageSize = pageSize,
            TotalItems = matched.Count,
            TotalPages = (long)Math.Ceiling(matched.Count / (double)pageSize),
        };
    }

    private static bool MatchesSearchText(Engineer engineer, string searchText)
    {
        return engineer.DisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || (engineer.Description?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false)
            || engineer.Tags.Any(tag => tag.Contains(searchText, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesAnyTag(Engineer engineer, List<string> tags)
    {
        return engineer.Tags.Any(tag => tags.Contains(tag, StringComparer.OrdinalIgnoreCase));
    }
}
