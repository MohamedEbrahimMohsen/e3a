using E3A.Domain.Teams;

namespace E3A.Application.Teams.Shared;

public static class TeamResultGenerator
{
    public static TeamResult Generate(Team team)
    {
        return new TeamResult(team.Id, team.Slug, team.DisplayName, team.Description, team.Tags, team.Status.ToString(), team.LatestVersionId, team.Members.Count, team.CreationDate, team.UpdationDate);
    }

    public static TeamDetailResult GenerateDetail(Team team)
    {
        var members = team.Members
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.EngineerId)
            .Select(x => new TeamMemberResult(x.EngineerId, x.EngineerSlug, x.PinnedVersionId, x.PinnedSemanticVersion, x.SortOrder))
            .ToList();

        return new TeamDetailResult(team.Id, team.Slug, team.DisplayName, team.Description, team.Tags, team.Status.ToString(), team.LatestVersionId, members, team.CreationDate, team.UpdationDate);
    }
}
