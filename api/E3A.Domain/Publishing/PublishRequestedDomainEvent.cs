using Core.DDD.Entities;

namespace E3A.Domain.Publishing;

public sealed record PublishRequestedDomainEvent(Guid VersionId) : DomainEvent();
