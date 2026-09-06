using E3A.Domain.Teams;

namespace E3A.Application.Catalog.Shared;

public static class CatalogTeamResultGenerator
{
    public static CatalogTeamResult Generate(Team team)
    {
        var memberSlugs = team.Members
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.EngineerId)
            .Select(x => x.EngineerSlug)
            .ToList();

        return new CatalogTeamResult(team.Id, team.Slug, team.DisplayName, team.Description, team.Tags, memberSlugs, team.LatestVersionId, team.CreationDate, team.UpdationDate);
    }
}
