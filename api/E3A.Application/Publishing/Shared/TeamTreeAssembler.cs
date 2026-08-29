using E3A.Application.Options;
using E3A.Domain.Teams;

namespace E3A.Application.Publishing.Shared;

public static class TeamTreeAssembler
{
    // Claude Code addresses a skill by its folder name, so two members' identically named skills must be given distinct folders.
    private const string NamespaceSeparator = "--";
    private const string SkillsRoot = "skills/";
    private static readonly string[] PrefixableRoots = ["agents/", "commands/"];

    public static List<PluginFile> Assemble(List<TeamMemberSnapshot> members, Team team, string semanticVersion, string authorName, PublishingOptions options)
    {
        List<(string MemberSlug, string Path, PluginFile File)> candidates = [];

        foreach (var member in members)
        {
            var allowed = new HashSet<string>(member.Manifest.Imported.Select(x => x.TargetPath).Concat(member.Manifest.Converted.Select(x => x.TargetPath)), StringComparer.OrdinalIgnoreCase);

            candidates.AddRange(member.SnapshotAssets
                .Where(asset => allowed.Contains(asset.Path) && IsCarriedRoot(asset.Path))
                .Select(asset => (member.MemberSlug, Path: NamespacePath(member.MemberSlug, asset.Path), File: asset)));
        }

        var collidingPaths = candidates
            .Where(candidate => !candidate.Path.StartsWith(SkillsRoot, StringComparison.OrdinalIgnoreCase))
            .GroupBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Select(candidate => candidate.MemberSlug).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        List<PluginFile> files = [.. candidates.Select(candidate => new PluginFile(collidingPaths.Contains(candidate.Path) ? PrefixFileName(candidate.MemberSlug, candidate.Path) : candidate.Path, candidate.File.Content))];
        files.Add(PluginJsonGenerator.Generate(team, semanticVersion, authorName, options));

        return [.. files.OrderBy(x => x.Path, StringComparer.Ordinal)];
    }

    private static bool IsCarriedRoot(string path)
    {
        return path.StartsWith(SkillsRoot, StringComparison.OrdinalIgnoreCase) || Array.Exists(PrefixableRoots, root => path.StartsWith(root, StringComparison.OrdinalIgnoreCase));
    }

    private static string NamespacePath(string memberSlug, string path)
    {
        return path.StartsWith(SkillsRoot, StringComparison.OrdinalIgnoreCase)
            ? $"{SkillsRoot}{memberSlug}{NamespaceSeparator}{path[SkillsRoot.Length..]}"
            : path;
    }

    private static string PrefixFileName(string memberSlug, string path)
    {
        var root = Array.Find(PrefixableRoots, x => path.StartsWith(x, StringComparison.OrdinalIgnoreCase))!;

        return $"{root}{memberSlug}{NamespaceSeparator}{path[root.Length..]}";
    }
}
