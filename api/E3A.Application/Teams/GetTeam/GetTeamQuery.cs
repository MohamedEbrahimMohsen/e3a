using E3A.Application.Teams.Shared;
using MediatR;

namespace E3A.Application.Teams.GetTeam;

public sealed record GetTeamQuery(Guid TeamId) : IRequest<TeamDetailResult>;
