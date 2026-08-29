using MediatR;

namespace E3A.Application.Teams.DeleteTeam;

public sealed record DeleteTeamCommand(Guid TeamId) : IRequest;
