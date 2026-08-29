using System.Text.Json.Serialization;

namespace E3A.Infrastructure.Authentication;

public sealed record GitHubAccessTokenPayload([property: JsonPropertyName("access_token")] string? AccessToken, [property: JsonPropertyName("error")] string? Error);
