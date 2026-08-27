using E3A.Application.Engineers.Shared;
using MediatR;

namespace E3A.Application.Engineers.ListEngineers;

public sealed record ListEngineersQuery : IRequest<List<EngineerResult>>;
