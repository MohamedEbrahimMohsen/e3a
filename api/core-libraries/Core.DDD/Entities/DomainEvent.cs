using MediatR;

namespace Core.DDD.Entities;

public record DomainEvent() : INotification;
