using E3A.Application.Engineers.Shared;
using MediatR;

namespace E3A.Application.Engineers.GetImportManifest;

public sealed record GetImportManifestQuery(Guid EngineerId) : IRequest<ImportManifestResult>;
