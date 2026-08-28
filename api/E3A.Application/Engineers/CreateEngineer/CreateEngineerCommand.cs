using E3A.Application.Engineers.Shared;
using MediatR;

namespace E3A.Application.Engineers.CreateEngineer;

public sealed record CreateEngineerCommand(string Slug, string DisplayName, string? Description, List<string> Tags) : IRequest<EngineerResult>;
