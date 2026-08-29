using E3A.Application.Shared;
using MediatR;

namespace E3A.Application.Teams.CheckTeamSlugAvailability;

public sealed record CheckTeamSlugAvailabilityQuery(string Slug) : IRequest<SlugAvailabilityResult>;
