using E3A.Application.Engineers.Shared;

namespace E3A.Application.Publishing.Shared;

public sealed record TeamMemberSnapshot(string MemberSlug, ImportManifestResult Manifest, List<PluginFile> SnapshotAssets);
