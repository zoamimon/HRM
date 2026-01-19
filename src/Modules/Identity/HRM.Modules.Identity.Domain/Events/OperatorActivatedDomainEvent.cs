using HRM.BuildingBlocks.Domain.Abstractions.Events;

namespace HRM.Modules.Identity.Domain.Events;

/// <summary>
/// Domain event raised when an operator is activated
/// Processed synchronously within the same transaction
///
/// Purpose:
/// - Notify domain event handlers
/// - Optionally create integration event for other modules
/// - Trigger activation workflows
///
/// Handler Responsibilities:
/// - Can create OperatorActivatedIntegrationEvent (optional)
/// - Notify Personnel module if needed
/// - Send activation notification email (future)
///
/// Business Context:
/// - Operator moves from Pending → Active status
/// - Can now login to the system
/// - Access permissions enabled
/// </summary>
public sealed record OperatorActivatedDomainEvent(
    Guid OperatorId,
    string Username
) : DomainEvent;
