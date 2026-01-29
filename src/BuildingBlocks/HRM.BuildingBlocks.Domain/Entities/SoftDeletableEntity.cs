using HRM.BuildingBlocks.Domain.Abstractions.SoftDelete;

namespace HRM.BuildingBlocks.Domain.Entities;

/// <summary>
/// Base class for entities that require both audit trail AND soft delete.
///
/// Extends AuditableEntity with:
/// - IsDeleted flag
/// - DeletedAtUtc timestamp
/// - Delete() / Restore() methods
///
/// Inheritance hierarchy:
/// - Entity: Id + DomainEvents only
/// - AuditableEntity: + Audit fields (CreatedAtUtc, ModifiedAtUtc, etc.)
/// - SoftDeletableEntity: + Soft delete (IsDeleted, DeletedAtUtc)
///
/// Use this for business entities that:
/// - Need full audit trail
/// - Cannot be physically deleted (compliance, referential integrity)
/// - Need ability to restore after deletion
///
/// Example entities: Employee, Contract, Role
///
/// For entities that don't need soft delete (e.g., Account, Permission),
/// use AuditableEntity directly.
/// </summary>
public abstract class SoftDeletableEntity : AuditableEntity, ISoftDeletable
{
    /// <summary>
    /// Indicates whether entity is soft-deleted.
    /// When true, entity is hidden from normal queries.
    /// </summary>
    public bool IsDeleted { get; protected set; }

    /// <summary>
    /// When the entity was soft-deleted (UTC).
    /// NULL if not deleted.
    /// </summary>
    public DateTime? DeletedAtUtc { get; protected set; }

    /// <summary>
    /// Performs soft delete operation.
    /// Marks entity as deleted without removing from database.
    ///
    /// Can be overridden to add:
    /// - Business rule validation
    /// - Domain events
    /// - Cascade soft delete to child entities
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
    /// Restores a soft-deleted entity.
    /// Makes entity visible in queries again.
    ///
    /// Can be overridden to add:
    /// - Business rule validation
    /// - Domain events
    /// - Cascade restore to child entities
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
}
