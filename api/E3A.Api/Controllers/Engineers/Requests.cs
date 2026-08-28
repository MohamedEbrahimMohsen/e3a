using System.Text.Json.Serialization;
using E3A.Domain.Publishing;

namespace E3A.Api.Controllers.Engineers;

public sealed record CreateEngineerRequest(string Slug, string DisplayName, string? Description, List<string>? Tags);

public sealed record UpdateEngineerRequest(string? Slug, string DisplayName, string? Description, List<string>? Tags);

public sealed record PublishEngineerRequest([property: JsonRequired] VersionIncrement Increment);
