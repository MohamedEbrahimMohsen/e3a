using E3A.Application.Publishing.Shared;
using MediatR;

namespace E3A.Application.Publishing.GetPublishStatus;

public sealed record GetPublishStatusQuery(Guid VersionId) : IRequest<PublishStatusResult>;
