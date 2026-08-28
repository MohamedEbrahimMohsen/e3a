using E3A.Application.Engineers.Shared;
using MediatR;

namespace E3A.Application.Engineers.RelistEngineer;

public sealed record RelistEngineerCommand(Guid EngineerId) : IRequest<EngineerResult>;
