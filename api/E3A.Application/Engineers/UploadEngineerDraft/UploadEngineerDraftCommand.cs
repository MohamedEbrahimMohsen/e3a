using E3A.Application.Engineers.Shared;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace E3A.Application.Engineers.UploadEngineerDraft;

public sealed record UploadEngineerDraftCommand(Guid EngineerId, IFormFile File) : IRequest<ImportManifestResult>;
