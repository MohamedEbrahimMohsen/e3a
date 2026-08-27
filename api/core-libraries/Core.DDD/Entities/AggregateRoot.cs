namespace Core.DDD.Entities;

/// <summary>
/// Marker for diffrentiate aggregators from entities, in addition to some additional properties.
/// </summary>
public abstract class AggregateRoot(Guid id) : Entity(id)
{
}