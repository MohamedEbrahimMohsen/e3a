using System.Text.Json.Serialization;

namespace E3A.Infrastructure.Authentication;

public sealed record GitHubProfilePayload([property: JsonPropertyName("id")] long Id, [property: JsonPropertyName("login")] string? Login, [property: JsonPropertyName("name")] string? Name, [property: JsonPropertyName("avatar_url")] string? AvatarUrl);
