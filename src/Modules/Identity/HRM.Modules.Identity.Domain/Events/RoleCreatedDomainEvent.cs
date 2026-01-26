using HRM.BuildingBlocks.Domain.Abstractions.Events;

namespace HRM.Modules.Identity.Domain.Events;

/// <summary>
/// Domain event raised when a new role is created
/// Processed synchronously within the same transaction
///
/// Purpose:
/// - Notify domain event handlers
/// - Create integration event for other modules
/// - Trigger audit logging
/// - Update cached permissions if needed
///
/// Handler Responsibilities:
/// - Create RoleCreatedIntegrationEvent
/// - Save to OutboxMessages table
/// - Publish asynchronously via OutboxProcessor
/// - Update permission cache
///
/// Subscribers (via Integration Event):
/// - Audit module: Log role creation
/// - Notifications module: Notify admins of new role (future)
/// - Cache invalidation: Clear role-related caches
/// </summary>
public sealed record RoleCreatedDomainEvent(
    Guid RoleId,
    string RoleName,
    string? Description,
    int PermissionCount
) : DomainEvent;
