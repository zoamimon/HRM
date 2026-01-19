using HRM.BuildingBlocks.Domain.Abstractions.Events;

namespace HRM.Modules.Identity.Domain.Events;

/// <summary>
/// Domain event raised when a new operator is registered
/// Processed synchronously within the same transaction
///
/// Purpose:
/// - Notify domain event handlers
/// - Create integration event for other modules
/// - Trigger cross-module workflows
///
/// Handler Responsibilities:
/// - Create OperatorRegisteredIntegrationEvent
/// - Save to OutboxMessages table
/// - Publish asynchronously via OutboxProcessor
///
/// Subscribers (via Integration Event):
/// - Personnel module: May create operator profile
/// - Notifications module: Send welcome email (future)
/// - Audit module: Log operator creation (future)
/// </summary>
public sealed record OperatorRegisteredDomainEvent(
    Guid OperatorId,
    string Username,
    string Email,
    string FullName
) : DomainEvent;
