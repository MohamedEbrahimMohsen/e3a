using E3A.Application.Engineers.Shared;
using MediatR;

namespace E3A.Application.Engineers.UpdateEngineer;

public sealed record UpdateEngineerCommand(Guid EngineerId, string DisplayName, string? Description, List<string> Tags) : IRequest<EngineerResult>;
