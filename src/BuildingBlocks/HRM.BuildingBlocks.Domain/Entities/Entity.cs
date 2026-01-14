using HRM.BuildingBlocks.Domain.Abstractions.Events;

namespace HRM.BuildingBlocks.Domain.Entities;

/// <summary>
/// Base class for all domain entities
/// Provides:
/// - Identity (Id)
/// - Audit trail (CreatedAtUtc, ModifiedAtUtc)
/// - Domain event collection
/// 
/// Entities are objects that have a distinct identity that runs through time and different states
/// Two entities are equal if they have the same Id and type, regardless of their attribute values
/// </summary>
public abstract class Entity
{
    /// <summary>
    /// Private list to store domain events
    /// Domain events are raised by entity operations and dispatched by UnitOfWork
    /// </summary>
    private readonly List<IDomainEvent> _domainEvents = new();

    /// <summary>
    /// Unique identifier for the entity
    /// Guid ensures uniqueness across distributed systems
    /// Protected init: Can only be set during initialization in derived classes
    /// </summary>
    public Guid Id { get; protected init; }

    /// <summary>
    /// When the entity was created (UTC)
    /// Set automatically in constructor
    /// Immutable after creation
    /// </summary>
    public DateTime CreatedAtUtc { get; protected set; }

    /// <summary>
    /// When the entity was last modified (UTC)
    /// NULL if never modified
    /// Updated via MarkAsModified() method or EF Core interceptor
    /// </summary>
    public DateTime? ModifiedAtUtc { get; protected set; }

    /// <summary>
    /// Read-only collection of domain events raised by this entity
    /// Events are dispatched during SaveChanges in the DbContext
    /// </summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Protected constructor for derived entities
    /// Sets CreatedAtUtc to current UTC time
    /// </summary>
    protected Entity()
    {
        CreatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Add a domain event to be dispatched
    /// Events represent something that happened in the domain
    /// 
    /// Examples:
    /// - OperatorRegisteredDomainEvent
    /// - UserCreatedDomainEvent
    /// - EmployeeAssignedDomainEvent
    /// 
    /// Events are dispatched synchronously during CommitAsync()
    /// </summary>
    /// <param name="domainEvent">The domain event to add</param>
    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Clear all domain events
    /// Called by UnitOfWork after events have been dispatched
    /// Prevents duplicate dispatch if SaveChanges called multiple times
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    /// <summary>
    /// Update modification timestamp
    /// Can be called:
    /// - Manually in domain logic
    /// - Automatically by EF Core SaveChangesInterceptor
    /// </summary>
    public void MarkAsModified()
    {
        ModifiedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Equality comparison based on Id and type
    /// Two entities are equal if:
    /// - Same type (GetType())
    /// - Same Id
    /// - Both are persisted (Id != Guid.Empty)
    /// </summary>
    public override bool Equals(object? obj)
    {
        if (obj is not Entity other)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (GetType() != other.GetType())
            return false;

        // Check if both entities are transient (not persisted yet)
        if (Id == Guid.Empty || other.Id == Guid.Empty)
            return false;

        return Id == other.Id;
    }

    /// <summary>
    /// Hash code based on Id
    /// Important for using entities in collections (HashSet, Dictionary)
    /// </summary>
    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    /// <summary>
    /// Equality operator overload
    /// </summary>
    public static bool operator ==(Entity? left, Entity? right)
    {
        if (left is null && right is null)
            return true;

        if (left is null || right is null)
            return false;

        return left.Equals(right);
    }

    /// <summary>
    /// Inequality operator overload
    /// </summary>
    public static bool operator !=(Entity? left, Entity? right)
    {
        return !(left == right);
    }
}
