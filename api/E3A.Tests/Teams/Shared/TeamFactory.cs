using E3A.Application.Options;
using E3A.Domain.Engineers;
using E3A.Domain.Publishing;
using E3A.Domain.Teams;
using E3A.Tests.Engineers.Shared;
using E3A.Tests.Publishing.Shared;

namespace E3A.Tests.Teams.Shared;

public sealed record TeamMemberFixture(Engineer Engineer, ItemVersion Version);

public static class TeamFactory
{
    public const string DefaultSlug = "dotnet-product-squad";
    public const string DefaultDisplayName = "DotNet Product Squad";

    public static Team Draft(Guid ownerUserId, string slug = DefaultSlug, string displayName = DefaultDisplayName, string? description = "A product squad.", List<string>? tags = null, DateTimeOffset? creationDate = null)
    {
        var team = Team.Create(ownerUserId, slug, displayName, description, tags ?? ["dotnet", "team"]);

        if (creationDate != null)
        {
            team.CreationDate = creationDate.Value;
        }

        return team;
    }

    public static Team WithMembers(Guid ownerUserId, params TeamMemberPin[] pins)
    {
        var team = Draft(ownerUserId);
        team.ReplaceMembers([.. pins], ownerUserId);

        return team;
    }

    public static Team Published(Guid ownerUserId, string slug = DefaultSlug)
    {
        var team = Draft(ownerUserId, slug);
        team.MarkPublished(Guid.NewGuid());

        return team;
    }

    public static TeamMemberPin Pin(string engineerSlug = "dive-backend-engineer", Guid? engineerId = null, Guid? versionId = null, string semanticVersion = "1.0.0")
    {
        return new TeamMemberPin(engineerId ?? Guid.NewGuid(), engineerSlug, versionId ?? Guid.NewGuid(), semanticVersion);
    }

    public static TeamMemberFixture PublishedMember(string engineerSlug, string semanticVersion = "1.0.0", Guid? ownerUserId = null)
    {
        var engineer = EngineerFactory.Draft(ownerUserId ?? Guid.NewGuid(), slug: engineerSlug);
        var version = ItemVersionFactory.Published(engineer.Id, semanticVersion: semanticVersion);
        engineer.MarkPublished(version.Id);

        return new TeamMemberFixture(engineer, version);
    }

    public static TeamsOptions CreateTeamsOptions(int maxTeamsPerCreator = 10, int maxMembersPerTeam = 10)
    {
        return new TeamsOptions
        {
            MaxTeamsPerCreator = maxTeamsPerCreator,
            MaxMembersPerTeam = maxMembersPerTeam,
            DisplayNameMaxLength = 100,
            DescriptionMaxLength = 500,
            SlugMaxLength = 100,
            SlugSuffixSize = 4,
            SlugMinLength = 3,
            MaxTags = 10,
            TagMaxLength = 30,
            TagsColumnMaxLength = 400,
            ReservedSlugs = ["e3a", "api", "admin", "www", "docs", "health", "install", "marketplace", "catalog", "teams", "new", "edit", "settings", "z", "m"],
        };
    }
}
