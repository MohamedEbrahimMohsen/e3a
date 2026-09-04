namespace E3A.Application.Authentication.Shared;

public sealed record CurrentUserResult(Guid Id, long? GitHubId, string? GitHubLogin, string? DisplayName, string? AvatarUrl, DateTimeOffset CreatedAt);
