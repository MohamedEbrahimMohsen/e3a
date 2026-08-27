using E3A.Application.Engineers.Shared;
using MediatR;

namespace E3A.Application.Engineers.GetEngineer;

public sealed record GetEngineerQuery(Guid EngineerId) : IRequest<EngineerResult>;
