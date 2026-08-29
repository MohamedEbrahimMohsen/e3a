using E3A.Domain.Engineers;
using E3A.Domain.Teams;

namespace E3A.Application.Publishing.Shared;

public sealed record PublishBuild(Engineer? Engineer, Team? Team, string PluginName, string AuthorName, List<PluginFile> Files, string? FailureReason);
