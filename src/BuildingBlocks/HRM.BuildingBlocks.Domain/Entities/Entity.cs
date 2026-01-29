using HRM.BuildingBlocks.Domain.Abstractions.Events;

namespace HRM.BuildingBlocks.Domain.Entities;

/// <summary>
/// Minimal base class for all domain entities.
///
/// Provides ONLY:
/// - Identity (Id)
/// - Domain event collection
/// - Equality semantics
///
/// All other concerns are opt-in via interfaces:
/// - IAuditableEntity → for audit trail (CreatedAtUtc, ModifiedAtUtc, etc.)
/// - ISoftDeletable → for soft delete support
///
/// For entities that need audit fields, extend AuditableEntity instead.
///
/// DDD Principle:
/// Entities are objects that have a distinct identity that runs through time.
/// Two entities are equal if they have the same Id and type, regardless of attributes.
/// </summary>
public abstract class Entity
{
    /// <summary>
    /// Private list to store domain events.
    /// Events are raised by entity operations and dispatched by UnitOfWork.
    /// </summary>
    private readonly List<IDomainEvent> _domainEvents = new();

    /// <summary>
    /// Unique identifier for the entity.
    /// Guid ensures uniqueness across distributed systems.
    /// </summary>
    public Guid Id { get; protected init; }

    /// <summary>
    /// Read-only collection of domain events raised by this entity.
    /// Events are dispatched during SaveChanges in the DbContext.
    /// </summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Add a domain event to be dispatched.
    /// Events represent something that happened in the domain.
    /// </summary>
    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Clear all domain events.
    /// Called by UnitOfWork after events have been dispatched.
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    /// <summary>
    /// Equality comparison based on Id and type.
    /// Two entities are equal if same type and same Id (both persisted).
    /// </summary>
    public override bool Equals(object? obj)
    {
        if (obj is not Entity other)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (GetType() != other.GetType())
            return false;

        // Transient entities are never equal
        if (Id == Guid.Empty || other.Id == Guid.Empty)
            return false;

        return Id == other.Id;
    }

    /// <summary>
    /// Hash code based on Id.
    /// </summary>
    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(Entity? left, Entity? right)
    {
        if (left is null && right is null)
            return true;
        if (left is null || right is null)
            return false;
        return left.Equals(right);
    }

    public static bool operator !=(Entity? left, Entity? right) => !(left == right);
}
