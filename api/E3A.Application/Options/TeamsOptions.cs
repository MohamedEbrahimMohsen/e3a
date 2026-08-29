namespace E3A.Application.Options;

public sealed class TeamsOptions
{
    public const string SectionName = "Teams";

    public int MaxTeamsPerCreator { get; set; }
    public int MaxMembersPerTeam { get; set; }
    public int DisplayNameMaxLength { get; set; }
    public int DescriptionMaxLength { get; set; }
    public int SlugMaxLength { get; set; }
    public int SlugSuffixSize { get; set; }
    public int SlugMinLength { get; set; }
    public int MaxTags { get; set; }
    public int TagMaxLength { get; set; }
    public int TagsColumnMaxLength { get; set; }
    public List<string> ReservedSlugs { get; set; } = [];
}
