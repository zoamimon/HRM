namespace HRM.BuildingBlocks.Domain.Abstractions.Audit;

/// <summary>
/// Interface for entities that support audit trail tracking.
///
/// Audit fields track:
/// - WHEN: CreatedAtUtc, ModifiedAtUtc
/// - WHO: CreatedById, ModifiedById
///
/// Implementation:
/// - Entity base class already implements these fields
/// - Derived classes that need to expose audit data should implement this interface
/// - AuditInterceptor in Infrastructure automatically sets these values
///
/// Note: This is a MARKER interface for entities that already inherit from Entity.
/// Entity base class contains the actual implementation.
/// Use this interface for type constraints and EF query filters.
/// </summary>
public interface IAuditableEntity
{
    /// <summary>
    /// When the entity was created (UTC)
    /// Set automatically on entity creation
    /// </summary>
    DateTime CreatedAtUtc { get; }

    /// <summary>
    /// When the entity was last modified (UTC)
    /// NULL if never modified since creation
    /// </summary>
    DateTime? ModifiedAtUtc { get; }

    /// <summary>
    /// Who created the entity (User/Account ID)
    /// NULL for system-created entities or legacy data
    /// </summary>
    Guid? CreatedById { get; }

    /// <summary>
    /// Who last modified the entity (User/Account ID)
    /// NULL if never modified or system modification
    /// </summary>
    Guid? ModifiedById { get; }
}
