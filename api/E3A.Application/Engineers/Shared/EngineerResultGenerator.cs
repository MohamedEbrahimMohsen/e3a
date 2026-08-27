using E3A.Domain.Engineers;

namespace E3A.Application.Engineers.Shared;

public static class EngineerResultGenerator
{
    public static EngineerResult Generate(Engineer engineer)
    {
        return new EngineerResult(engineer.Id, engineer.Slug, engineer.DisplayName, engineer.Description, engineer.Tags, engineer.Status.ToString(), engineer.LatestVersionId, engineer.InstallCount, engineer.CreationDate, engineer.UpdationDate);
    }
}
