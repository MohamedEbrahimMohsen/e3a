namespace E3a.Core.Options;

public sealed class PublishingOptions
{
    public const string SectionName = "Publishing";

    public int MaxFilesPerSkill { get; set; }
    public long MaxBytesPerSkill { get; set; }
    public int MaxSkillSlugLength { get; set; }
    public List<string> AllowedSkillExtensions { get; set; } = [];
    public int MaxEngineersPerCreator { get; set; }
    public int MaxTeamsPerCreator { get; set; }
    public int MaxVersionsPerItem { get; set; }
}
