namespace E3A.Application.Shared;

public sealed record SlugAvailabilityResult(string Slug, bool IsAvailable, string? SuggestedSlug);
