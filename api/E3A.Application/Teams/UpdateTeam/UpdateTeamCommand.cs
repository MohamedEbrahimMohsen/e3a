using E3A.Application.Teams.Shared;
using MediatR;

namespace E3A.Application.Teams.UpdateTeam;

public sealed record UpdateTeamCommand(Guid TeamId, string? Slug, string DisplayName, string? Description, List<string> Tags) : IRequest<TeamResult>;
