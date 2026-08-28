using E3A.Application.Engineers.Shared;
using MediatR;

namespace E3A.Application.Engineers.CheckSlugAvailability;

public sealed record CheckSlugAvailabilityQuery(string Slug) : IRequest<SlugAvailabilityResult>;
