namespace E3A.Application.Engineers.Shared;

public sealed record SlugAvailabilityResult(string Slug, bool IsAvailable, string? SuggestedSlug);
