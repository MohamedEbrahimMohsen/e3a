using System.Globalization;
using System.Text;
using E3A.Application.Engineers.Shared;

namespace E3A.Application.Engineers.UploadEngineerDraft;

public sealed record HouseRulesGeneration(UploadedFile SkillFile, List<ConvertedItemResult> Converted, string ClaudeMdSnippet);

public static class HouseRulesSkillGenerator
{
    public const string MergedIntoHouseRulesReason = "Merged into the generated house-rules skill; always-on and path-scoped behaviour becomes trigger-based.";
    public const string ClaudeMdSnippet = "Always read and follow the house-rules skill before doing any work in this project.";

    // Skills are addressed as skills/{folder}/SKILL.md (docs/plugin-spec.md); the front matter name must match the folder.
    private const string SkillsRootName = "skills";
    private const string SkillFileName = "SKILL.md";
    private const string SkillDescription = "House rules imported from this creator's CLAUDE.md and rules. Use at the start of every task and whenever writing, reviewing, or planning work in this project so it follows these standards.";

    public static HouseRulesGeneration Generate(List<UploadedFile> sources, string skillFolderName)
    {
        var content = new StringBuilder();
        content.AppendLine("---");
        content.AppendLine(CultureInfo.InvariantCulture, $"name: {skillFolderName}");
        content.AppendLine(CultureInfo.InvariantCulture, $"description: {SkillDescription}");
        content.AppendLine("---");
        content.AppendLine();
        content.AppendLine("# House Rules");

        List<ConvertedItemResult> converted = [];
        var targetPath = $"{SkillsRootName}/{skillFolderName}/{SkillFileName}";

        foreach (var source in sources)
        {
            content.AppendLine();
            content.AppendLine(CultureInfo.InvariantCulture, $"## Source: {source.Path}");
            content.AppendLine();
            content.AppendLine(Encoding.UTF8.GetString(source.Content));
            converted.Add(new ConvertedItemResult(source.Path, targetPath, MergedIntoHouseRulesReason));
        }

        return new HouseRulesGeneration(new UploadedFile(targetPath, Encoding.UTF8.GetBytes(content.ToString())), converted, ClaudeMdSnippet);
    }
}
