using E3A.Application.Publishing.Shared;
using E3A.Domain.Publishing;
using MediatR;

namespace E3A.Application.Teams.PublishTeam;

public sealed record PublishTeamCommand(Guid TeamId, VersionIncrement Increment) : IRequest<PublishStatusResult>;
