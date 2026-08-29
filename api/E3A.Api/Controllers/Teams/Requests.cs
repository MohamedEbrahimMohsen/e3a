using System.Text.Json.Serialization;
using E3A.Domain.Publishing;

namespace E3A.Api.Controllers.Teams;

public sealed record CreateTeamRequest(string Slug, string DisplayName, string? Description, List<string>? Tags);

public sealed record UpdateTeamRequest(string? Slug, string DisplayName, string? Description, List<string>? Tags);

public sealed record SetTeamMembersRequest(List<TeamMemberRequest>? Members);

public sealed record TeamMemberRequest(Guid EngineerId, Guid? PinnedVersionId);

public sealed record PublishTeamRequest([property: JsonRequired] VersionIncrement Increment);
