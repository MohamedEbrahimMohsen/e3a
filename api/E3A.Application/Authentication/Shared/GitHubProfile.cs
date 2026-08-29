namespace E3A.Application.Authentication.Shared;

public sealed record GitHubProfile(long Id, string Login, string? Name, string? AvatarUrl);
