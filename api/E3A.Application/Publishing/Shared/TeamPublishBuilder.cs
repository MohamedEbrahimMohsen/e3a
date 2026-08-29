using System.Text.Json;
using Core.Azure.Clients;
using E3A.Application.Engineers.Shared;
using E3A.Application.Exceptions;
using E3A.Application.Options;
using E3A.Application.Teams.Shared;
using E3A.Domain.Identity;
using E3A.Domain.Publishing;
using E3A.Domain.Teams;

namespace E3A.Application.Publishing.Shared;

public static class TeamPublishBuilder
{
    public static async Task<PublishBuild> BuildAsync(ITeamRepository teamRepository, IItemVersionRepository itemVersionRepository, IUserRepository userRepository, IStorageBlobClient storageBlobClient, AzureOptions azureOptions, PublishingOptions publishingOptions, ItemVersion version, CancellationToken cancellationToken)
    {
        var team = await teamRepository.GetByIdAsync(version.ItemId, cancellationToken).ConfigureAwait(false);

        if (team == null)
        {
            return Failed(ErrorCodes.TeamNotFound);
        }

        var roster = JsonSerializer.Deserialize<TeamRosterResult>(version.FrozenManifestJson);

        if (roster == null)
        {
            return Failed(ErrorCodes.TeamRosterInvalid);
        }

        if (roster.Members.Count == 0)
        {
            return Failed(ErrorCodes.TeamEmpty);
        }

        var orderedMembers = roster.Members.OrderBy(x => x.SortOrder).ThenBy(x => x.EngineerId).ToList();
        var versionIds = orderedMembers.Select(x => x.PinnedVersionId).ToList();
        var memberVersions = await itemVersionRepository.FindAsync(x => versionIds.Contains(x.Id), cancellationToken, asNoTracking: true).ConfigureAwait(false);
        List<TeamMemberSnapshot> snapshots = [];

        foreach (var member in orderedMembers)
        {
            var memberVersion = memberVersions.Find(x => x.Id == member.PinnedVersionId);

            if (memberVersion == null || memberVersion.ItemType != ItemType.Engineer || memberVersion.ItemId != member.EngineerId || memberVersion.Status != ItemVersionStatus.Published)
            {
                return Failed(ErrorCodes.TeamMemberVersionNotPublished);
            }

            var manifest = JsonSerializer.Deserialize<ImportManifestResult>(memberVersion.FrozenManifestJson);

            if (manifest == null)
            {
                return Failed(ErrorCodes.TeamMemberManifestInvalid);
            }

            var assets = await TeamSnapshotReader.ReadAsync(storageBlobClient, azureOptions, member.PinnedVersionId, cancellationToken).ConfigureAwait(false);

            if (assets.Count == 0)
            {
                return Failed(ErrorCodes.TeamMemberSnapshotEmpty);
            }

            snapshots.Add(new TeamMemberSnapshot(member.EngineerSlug, manifest, assets));
        }

        var user = await userRepository.GetByIdAsync(team.OwnerUserId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var authorName = string.IsNullOrWhiteSpace(user?.UserName) ? team.Slug : user.UserName;
        var files = TeamTreeAssembler.Assemble(snapshots, team, version.SemanticVersion, authorName, publishingOptions);
        var errors = PluginStructureValidator.Validate(files, publishingOptions);

        return errors.Count > 0
            ? Failed(string.Join(", ", errors))
            : new PublishBuild(null, team, PluginName.ForTeam(team.Slug), authorName, files, null);
    }

    private static PublishBuild Failed(string failureReason)
    {
        return new PublishBuild(null, null, string.Empty, string.Empty, [], failureReason);
    }
}
