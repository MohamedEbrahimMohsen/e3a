using MediatR;

namespace E3A.Application.Publishing.ProcessPublishJob;

public sealed record ProcessPublishJobCommand(Guid VersionId) : IRequest;
