using MediatR;

namespace E3A.Application.Engineers.DeleteEngineer;

public sealed record DeleteEngineerCommand(Guid EngineerId) : IRequest;
