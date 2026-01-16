using HRM.BuildingBlocks.Domain.Abstractions.Events;
using HRM.BuildingBlocks.Domain.Abstractions.SoftDelete;

namespace HRM.BuildingBlocks.Domain.Entities;

/// <summary>
/// Base class for all domain entities
/// Provides:
/// - Identity (Id)
/// - Enhanced audit trail (CreatedAtUtc, ModifiedAtUtc, CreatedById, ModifiedById)
/// - Soft delete support (IsDeleted, DeletedAtUtc)
/// - Domain event collection
///
/// Entities are objects that have a distinct identity that runs through time and different states
/// Two entities are equal if they have the same Id and type, regardless of their attribute values
///
/// New Features:
/// - Enhanced Audit: Track WHO created/modified entities (CreatedById, ModifiedById)
/// - Soft Delete: Logical deletion with ability to restore (ISoftDeletable)
/// </summary>
public abstract class Entity : ISoftDeletable
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
    /// Who created the entity (User/Operator ID)
    /// NULL for system-created entities or legacy data
    /// Set automatically by AuditInterceptor on entity creation
    ///
    /// Use Cases:
    /// - Audit: Who created this employee record?
    /// - Compliance: Track user actions for GDPR/regulations
    /// - Analytics: Most active users creating records
    /// - Security: Trace data origin for security investigations
    /// </summary>
    public Guid? CreatedById { get; protected set; }

    /// <summary>
    /// Who last modified the entity (User/Operator ID)
    /// NULL if never modified or legacy data
    /// Updated automatically by AuditInterceptor on entity modification
    ///
    /// Use Cases:
    /// - Audit: Who last changed this record?
    /// - Accountability: Track responsibility for changes
    /// - Change History: Combined with ModifiedAtUtc for full audit
    /// </summary>
    public Guid? ModifiedById { get; protected set; }

    /// <summary>
    /// Indicates whether entity is soft-deleted
    /// False = Active (default)
    /// True = Deleted (hidden from normal queries)
    ///
    /// Soft Delete Benefits:
    /// - Data recovery capability
    /// - Audit trail preservation
    /// - Maintains referential integrity
    /// - Compliance with data retention policies
    /// </summary>
    public bool IsDeleted { get; protected set; }

    /// <summary>
    /// When the entity was soft-deleted (UTC)
    /// NULL = Not deleted (active)
    /// DateTime = Deletion timestamp
    ///
    /// Use Cases:
    /// - Restore: Show user when they deleted it
    /// - Cleanup: Permanently delete old soft-deleted records
    /// - Audit: Deletion history
    /// </summary>
    public DateTime? DeletedAtUtc { get; protected set; }

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
    /// Update modification timestamp (backward compatibility)
    /// Used by AuditInterceptor when user context is not available
    /// </summary>
    public void MarkAsModified()
    {
        ModifiedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Update modification timestamp and track who modified
    /// Preferred method when user context is available
    /// Called automatically by AuditInterceptor with current user ID
    /// </summary>
    /// <param name="modifiedById">ID of user/operator making the modification</param>
    public void MarkAsModified(Guid modifiedById)
    {
        ModifiedAtUtc = DateTime.UtcNow;
        ModifiedById = modifiedById;
    }

    /// <summary>
    /// Set the creator ID
    /// Called automatically by AuditInterceptor on entity creation
    /// Should NOT be called manually in domain logic
    /// </summary>
    /// <param name="createdById">ID of user/operator creating the entity</param>
    internal void SetCreatedBy(Guid createdById)
    {
        CreatedById = createdById;
    }

    /// <summary>
    /// Performs soft delete operation
    /// Marks entity as deleted without removing from database
    ///
    /// Can be overridden in derived classes to add:
    /// - Business rule validation
    /// - Domain events
    /// - Cascade soft delete to child entities
    ///
    /// Example Override:
    /// <code>
    /// public override void Delete()
    /// {
    ///     if (HasActiveAssignments)
    ///         throw new DomainException("Cannot delete employee with active assignments");
    ///
    ///     base.Delete();
    ///     AddDomainEvent(new EmployeeDeletedDomainEvent(Id));
    /// }
    /// </code>
    /// </summary>
    public virtual void Delete()
    {
        if (IsDeleted)
        {
            throw new InvalidOperationException("Entity is already deleted");
        }

        IsDeleted = true;
        DeletedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Restores a soft-deleted entity
    /// Makes entity visible in queries again
    ///
    /// Can be overridden in derived classes to add:
    /// - Business rule validation
    /// - Domain events
    /// - Cascade restore to child entities
    ///
    /// Example Override:
    /// <code>
    /// public override void Restore()
    /// {
    ///     if (!CanBeRestored())
    ///         throw new DomainException("Entity cannot be restored at this time");
    ///
    ///     base.Restore();
    ///     AddDomainEvent(new EmployeeRestoredDomainEvent(Id));
    /// }
    /// </code>
    /// </summary>
    public virtual void Restore()
    {
        if (!IsDeleted)
        {
            throw new InvalidOperationException("Entity is not deleted");
        }

        IsDeleted = false;
        DeletedAtUtc = null;
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
