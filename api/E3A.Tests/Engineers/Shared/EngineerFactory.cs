using E3A.Application.Options;
using E3A.Domain.Engineers;

namespace E3A.Tests.Engineers.Shared;

public static class EngineerFactory
{
    public const string DefaultDisplayName = "Dive Backend Engineer";
    public const string DefaultSlug = "dive-backend-engineer";

    public static Engineer Draft(Guid ownerUserId, string displayName = DefaultDisplayName, string slug = DefaultSlug, DateTimeOffset? creationDate = null)
    {
        var engineer = Engineer.Create(ownerUserId, slug, displayName, "A backend engineer.", ["dotnet", "ddd"]);

        if (creationDate != null)
        {
            engineer.CreationDate = creationDate.Value;
        }

        return engineer;
    }

    public static Engineer Published(Guid ownerUserId, string slug = DefaultSlug, string displayName = DefaultDisplayName, string? description = "A backend engineer.", List<string>? tags = null, int installCount = 0, DateTimeOffset? creationDate = null)
    {
        var engineer = Engineer.Create(ownerUserId, slug, displayName, description, tags ?? ["dotnet", "ddd"]);
        engineer.MarkPublished(Guid.NewGuid());
        engineer.RecordInstallCount(installCount);

        if (creationDate != null)
        {
            engineer.CreationDate = creationDate.Value;
        }

        return engineer;
    }

    public static EngineersOptions CreateEngineersOptions(int maxEngineersPerCreator = 50)
    {
        return new EngineersOptions
        {
            MaxEngineersPerCreator = maxEngineersPerCreator,
            DisplayNameMaxLength = 100,
            DescriptionMaxLength = 500,
            SlugMaxLength = 100,
            SlugSuffixSize = 4,
            MaxTags = 10,
            TagMaxLength = 30,
            TagsColumnMaxLength = 400,
        };
    }
}
