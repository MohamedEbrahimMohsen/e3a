using E3A.Application.Options;
using E3A.Application.Publishing.Security;
using E3A.Domain.Publishing;

namespace E3A.Application.Publishing.Shared;

public static class PublishStatusResultGenerator
{
    public static PublishStatusResult Generate(ItemVersion version, PublishingOptions options)
    {
        var zipUrl = version.ZipBlobPath == null ? null : PublishBlobPaths.ZipUrl(options.PublicSiteUrl, version.ZipBlobPath);

        return new PublishStatusResult(version.Id, version.ItemId, version.ItemType.ToString(), version.VersionNumber, version.SemanticVersion, version.Status.ToString(), zipUrl, version.ZipSha256, version.SizeBytes, version.FailureReason, ScanReportSerializer.Deserialize(version.ScanReportJson), version.UpdationDate);
    }
}
