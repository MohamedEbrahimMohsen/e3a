using E3A.Application.Publishing.Shared;
using E3A.Domain.Publishing;
using MediatR;

namespace E3A.Application.Engineers.PublishEngineer;

public sealed record PublishEngineerCommand(Guid EngineerId, VersionIncrement Increment) : IRequest<PublishStatusResult>;
