namespace E3A.Api.Controllers.Engineers;

public sealed record CreateEngineerRequest(string DisplayName, string? Description, List<string>? Tags);

public sealed record UpdateEngineerRequest(string DisplayName, string? Description, List<string>? Tags);
