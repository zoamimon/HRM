using HRM.BuildingBlocks.Domain.Abstractions.Audit;

namespace HRM.BuildingBlocks.Domain.Entities;

/// <summary>
/// Base class for entities that require audit trail.
///
/// Extends Entity with:
/// - CreatedAtUtc, ModifiedAtUtc (WHEN)
/// - CreatedById, ModifiedById (WHO)
///
/// Use this as base class for most business entities.
/// For entities that don't need audit (e.g., lookup tables), use Entity directly.
///
/// Soft delete is NOT included - implement ISoftDeletable separately if needed.
/// </summary>
public abstract class AuditableEntity : Entity, IAuditableEntity
{
    /// <summary>
    /// When the entity was created (UTC).
    /// Set automatically in constructor.
    /// </summary>
    public DateTime CreatedAtUtc { get; protected set; }

    /// <summary>
    /// When the entity was last modified (UTC).
    /// NULL if never modified since creation.
    /// </summary>
    public DateTime? ModifiedAtUtc { get; protected set; }

    /// <summary>
    /// Who created the entity (Account ID).
    /// NULL for system-created entities or legacy data.
    /// Set by AuditInterceptor.
    /// </summary>
    public Guid? CreatedById { get; protected set; }

    /// <summary>
    /// Who last modified the entity (Account ID).
    /// NULL if never modified.
    /// Set by AuditInterceptor.
    /// </summary>
    public Guid? ModifiedById { get; protected set; }

    /// <summary>
    /// Protected constructor sets CreatedAtUtc.
    /// </summary>
    protected AuditableEntity()
    {
        CreatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Update modification timestamp.
    /// Used when user context is not available.
    /// </summary>
    public void MarkAsModified()
    {
        ModifiedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Update modification timestamp with user tracking.
    /// Preferred method when user context is available.
    /// </summary>
    public void MarkAsModified(Guid modifiedById)
    {
        ModifiedAtUtc = DateTime.UtcNow;
        ModifiedById = modifiedById;
    }

    /// <summary>
    /// Set the creator ID.
    /// Called by AuditInterceptor on entity creation.
    /// Should NOT be called manually in domain logic.
    /// </summary>
    public void SetCreatedBy(Guid createdById)
    {
        CreatedById = createdById;
    }
}
