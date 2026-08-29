using E3A.Domain.Teams;

namespace E3A.Application.Teams.Shared;

public static class TeamRosterGenerator
{
    public static TeamRosterResult Generate(Team team)
    {
        var members = team.Members
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.EngineerId)
            .Select(x => new TeamRosterMemberResult(x.EngineerId, x.EngineerSlug, x.PinnedVersionId, x.PinnedSemanticVersion, x.SortOrder))
            .ToList();

        return new TeamRosterResult(members);
    }
}
