using E3A.Application.Teams.Shared;
using MediatR;

namespace E3A.Application.Teams.CreateTeam;

public sealed record CreateTeamCommand(string Slug, string DisplayName, string? Description, List<string> Tags) : IRequest<TeamResult>;
