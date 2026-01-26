using HRM.BuildingBlocks.Domain.Abstractions.Events;

namespace HRM.Modules.Identity.Domain.Events;

/// <summary>
/// Domain event raised when a role is deleted (soft delete)
/// Processed synchronously within the same transaction
///
/// Purpose:
/// - Notify domain event handlers
/// - Create integration event for other modules
/// - Trigger audit logging
/// - Remove cached permissions
///
/// Handler Responsibilities:
/// - Create RoleDeletedIntegrationEvent
/// - Save to OutboxMessages table
/// - Publish asynchronously via OutboxProcessor
/// - Clear permission cache
/// - Validate no active user assignments
///
/// Subscribers (via Integration Event):
/// - Audit module: Log role deletion
/// - Cache invalidation: Clear role-related caches
/// - User module: Ensure no users have this role assigned (validation)
/// </summary>
public sealed record RoleDeletedDomainEvent(
    Guid RoleId,
    string RoleName
) : DomainEvent;
