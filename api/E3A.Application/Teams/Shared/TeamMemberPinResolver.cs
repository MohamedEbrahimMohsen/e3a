using Core.Errors;
using E3A.Application.Exceptions;
using E3A.Application.Teams.SetTeamMembers;
using E3A.Domain.Engineers;
using E3A.Domain.Publishing;
using E3A.Domain.Teams;

namespace E3A.Application.Teams.Shared;

public static class TeamMemberPinResolver
{
    public static List<Guid> ResolveVersionIds(List<TeamMemberSelection> selections, List<Engineer> engineers, List<TeamMember> existingMembers)
    {
        return [.. selections.Select(selection => ResolveVersionId(selection, FindEngineer(selection, engineers), existingMembers))];
    }

    public static List<TeamMemberPin> ResolvePins(List<TeamMemberSelection> selections, List<Engineer> engineers, List<ItemVersion> versions, List<TeamMember> existingMembers)
    {
        var pins = new List<TeamMemberPin>();

        foreach (var selection in selections)
        {
            var engineer = FindEngineer(selection, engineers);
            var versionId = ResolveVersionId(selection, engineer, existingMembers);
            var version = versions.Find(x => x.Id == versionId);

            if (version == null || version.ItemType != ItemType.Engineer || version.ItemId != engineer.Id || version.Status != ItemVersionStatus.Published)
            {
                throw new BusinessRuleViolationCoreException(ErrorCodes.TeamMemberVersionNotPublished, context: new Dictionary<string, object> { ["engineerId"] = selection.EngineerId });
            }

            pins.Add(new TeamMemberPin(engineer.Id, engineer.Slug, version.Id, version.SemanticVersion));
        }

        return pins;
    }

    private static Engineer FindEngineer(TeamMemberSelection selection, List<Engineer> engineers)
    {
        return engineers.Find(x => x.Id == selection.EngineerId) ?? throw new NotFoundCoreException(ErrorCodes.EngineerNotFound);
    }

    private static Guid ResolveVersionId(TeamMemberSelection selection, Engineer engineer, List<TeamMember> existingMembers)
    {
        var existingPin = existingMembers.Find(x => x.EngineerId == selection.EngineerId)?.PinnedVersionId;

        return selection.PinnedVersionId ?? existingPin ?? engineer.LatestVersionId
            ?? throw new BusinessRuleViolationCoreException(ErrorCodes.TeamMemberNotPublished, context: new Dictionary<string, object> { ["engineerId"] = selection.EngineerId });
    }
}
