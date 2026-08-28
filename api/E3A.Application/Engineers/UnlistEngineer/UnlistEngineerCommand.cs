using E3A.Application.Engineers.Shared;
using MediatR;

namespace E3A.Application.Engineers.UnlistEngineer;

public sealed record UnlistEngineerCommand(Guid EngineerId) : IRequest<EngineerResult>;
