using E3A.Application.Engineers.Shared;
using E3A.Domain.Engineers;
using System.Text.Json;

namespace E3A.Application.Catalog.Shared;

public static class CatalogEngineerResultGenerator
{
    public static CatalogEngineerResult Generate(Engineer engineer)
    {
        return new CatalogEngineerResult(engineer.Id, engineer.Slug, engineer.DisplayName, engineer.Description, engineer.Tags, engineer.InstallCount, engineer.LatestVersionId, engineer.CreationDate, engineer.UpdationDate);
    }

    public static CatalogEngineerDetailResult GenerateDetail(Engineer engineer)
    {
        List<HookWarningResult> hookWarnings = engineer.DraftManifestJson == null ? [] : JsonSerializer.Deserialize<ImportManifestResult>(engineer.DraftManifestJson)!.HookWarnings;

        return new CatalogEngineerDetailResult(engineer.Id, engineer.Slug, engineer.DisplayName, engineer.Description, engineer.Tags, engineer.InstallCount, engineer.OwnerUserId, engineer.LatestVersionId, hookWarnings, engineer.CreationDate, engineer.UpdationDate);
    }
}
