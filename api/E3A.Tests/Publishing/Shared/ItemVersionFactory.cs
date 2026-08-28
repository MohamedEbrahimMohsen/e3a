using E3A.Domain.Publishing;

namespace E3A.Tests.Publishing.Shared;

public static class ItemVersionFactory
{
    public const string DefaultSemanticVersion = "1.0.0";
    public const string DefaultFrozenManifestJson = "{}";
    public const string DefaultZipBlobPath = "z/e3a-dive-backend-engineer/1.0.0.zip";
    public const string DefaultZipSha256 = "2c26b46b68ffc68ff99b453c1d30413413422d706483bfa0f98a5e886266e7ae";
    public const long DefaultSizeBytes = 2048;

    public static ItemVersion Queued(Guid engineerId, int versionNumber = 1, string semanticVersion = DefaultSemanticVersion, string frozenManifestJson = DefaultFrozenManifestJson)
    {
        return ItemVersion.Create(ItemType.Engineer, engineerId, versionNumber, semanticVersion, frozenManifestJson, Guid.NewGuid());
    }

    public static ItemVersion Building(Guid engineerId, int versionNumber = 1, string semanticVersion = DefaultSemanticVersion, string frozenManifestJson = DefaultFrozenManifestJson)
    {
        var version = Queued(engineerId, versionNumber, semanticVersion, frozenManifestJson);
        version.MarkBuilding();

        return version;
    }

    public static ItemVersion Published(Guid engineerId, int versionNumber = 1, string semanticVersion = DefaultSemanticVersion, string zipBlobPath = DefaultZipBlobPath)
    {
        var version = Queued(engineerId, versionNumber, semanticVersion);
        version.MarkPublished(zipBlobPath, DefaultZipSha256, DefaultSizeBytes);

        return version;
    }

    public static ItemVersion Failed(Guid engineerId, string failureReason, int versionNumber = 1, string semanticVersion = DefaultSemanticVersion)
    {
        var version = Queued(engineerId, versionNumber, semanticVersion);
        version.MarkFailed(failureReason);

        return version;
    }
}
