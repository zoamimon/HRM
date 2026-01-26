using HRM.BuildingBlocks.Domain.Abstractions.Events;

namespace HRM.Modules.Identity.Domain.Events;

/// <summary>
/// Domain event raised when permissions are added to or removed from a role
/// Processed synchronously within the same transaction
///
/// Purpose:
/// - Notify domain event handlers
/// - Create integration event for other modules
/// - Trigger permission cache invalidation
/// - Update affected user permissions
///
/// Handler Responsibilities:
/// - Create RolePermissionsModifiedIntegrationEvent
/// - Save to OutboxMessages table
/// - Publish asynchronously via OutboxProcessor
/// - Invalidate permission cache for users with this role
///
/// Subscribers (via Integration Event):
/// - Cache invalidation: Clear permission caches for affected users
/// - User module: Refresh runtime permissions for active users with this role
/// - Audit module: Log permission changes
/// </summary>
public sealed record RolePermissionsModifiedDomainEvent(
    Guid RoleId,
    string RoleName,
    int PermissionsAdded,
    int PermissionsRemoved,
    int TotalPermissions
) : DomainEvent;
