using HRM.BuildingBlocks.Domain.Abstractions.Events;

namespace HRM.Modules.Identity.Domain.Events;

/// <summary>
/// Domain event raised when a role is updated
/// Processed synchronously within the same transaction
///
/// Purpose:
/// - Notify domain event handlers
/// - Create integration event for other modules
/// - Trigger audit logging
/// - Update cached permissions
///
/// Handler Responsibilities:
/// - Create RoleUpdatedIntegrationEvent
/// - Save to OutboxMessages table
/// - Publish asynchronously via OutboxProcessor
/// - Invalidate permission cache for affected users
///
/// Subscribers (via Integration Event):
/// - Audit module: Log role modifications
/// - Cache invalidation: Clear role caches and affected user permission caches
/// - User module: Refresh user permissions if role is assigned
/// </summary>
public sealed record RoleUpdatedDomainEvent(
    Guid RoleId,
    string RoleName,
    int PermissionCount
) : DomainEvent;
