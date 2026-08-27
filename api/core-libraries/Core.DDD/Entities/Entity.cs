using System.ComponentModel.DataAnnotations;

namespace Core.DDD.Entities;

/// <summary>
/// Base entitiy that every Entity or Aggregator in the application should inherit from it.
/// All entities will be int Id based for simplicity.
/// </summary>
public abstract class Entity(Guid id) : IEntity, ISoftDeletable
{
    [Key]
    public Guid Id { get; set; } = id;
    public bool IsDeleted { get; set; } = false;
    public DateTimeOffset? DeletedAt { get; set; }

    private readonly List<DomainEvent> _domainEvents = [];
    public void RaiseDomainEvent(DomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public List<DomainEvent> GetDomainEvents() => _domainEvents;
    public void ClearDomainEvents() => _domainEvents.Clear();
    
    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAt = null;
    }
}

public interface IEntity
{
    Guid Id { get; set; }
    bool IsDeleted { get; set; }
    DateTimeOffset? DeletedAt { get; set; }
}
