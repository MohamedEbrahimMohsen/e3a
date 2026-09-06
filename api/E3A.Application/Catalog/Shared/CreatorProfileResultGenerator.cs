using E3A.Domain.Engineers;
using E3A.Domain.Identity;
using E3A.Domain.Teams;

namespace E3A.Application.Catalog.Shared;

public static class CreatorProfileResultGenerator
{
    public static CreatorProfileResult Generate(User creator, List<Engineer> engineers, List<Team> teams)
    {
        var engineerResults = engineers
            .Select(CatalogEngineerResultGenerator.Generate)
            .ToList();

        var teamResults = teams
            .Select(CatalogTeamResultGenerator.Generate)
            .ToList();

        var totalInstalls = engineerResults.Sum(x => x.InstallCount);

        // The creator was matched on GitHubLogin, so it cannot be null here.
        return new CreatorProfileResult(creator.Id, creator.GitHubLogin!, creator.DisplayName, creator.AvatarUrl, creator.CreationDate, totalInstalls, engineerResults, teamResults);
    }
}
