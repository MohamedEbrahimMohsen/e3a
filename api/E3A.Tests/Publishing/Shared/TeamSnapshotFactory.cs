using System.Text.Json;
using E3A.Application.Publishing.Shared;
using E3A.Application.Teams.Shared;

namespace E3A.Tests.Publishing.Shared;

public static class TeamSnapshotFactory
{
    public static TeamRosterResult Roster(params TeamRosterMemberResult[] members)
    {
        return new TeamRosterResult([.. members]);
    }

    public static string RosterJson(params TeamRosterMemberResult[] members)
    {
        return JsonSerializer.Serialize(Roster(members));
    }

    public static TeamMemberSnapshot MemberSnapshot(string memberSlug, params string[] paths)
    {
        return new TeamMemberSnapshot(memberSlug, PluginFileFactory.Manifest(paths), PluginFileFactory.Files(paths));
    }
}
