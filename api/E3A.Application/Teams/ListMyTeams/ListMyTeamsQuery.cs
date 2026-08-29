using E3A.Application.Teams.Shared;
using MediatR;

namespace E3A.Application.Teams.ListMyTeams;

public sealed record ListMyTeamsQuery : IRequest<List<TeamResult>>;
