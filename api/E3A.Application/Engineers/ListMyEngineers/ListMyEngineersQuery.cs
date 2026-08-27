using E3A.Application.Engineers.Shared;
using MediatR;

namespace E3A.Application.Engineers.ListMyEngineers;

public sealed record ListMyEngineersQuery : IRequest<List<EngineerResult>>;
