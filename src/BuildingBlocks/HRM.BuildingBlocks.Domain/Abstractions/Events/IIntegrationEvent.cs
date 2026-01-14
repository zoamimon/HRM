using MediatR;

namespace HRM.BuildingBlocks.Domain.Abstractions.Events;

/// <summary>
/// Interface for integration events (cross-module communication)
/// 
/// Integration events enable communication between different bounded contexts/modules
/// They are published asynchronously via event bus after successful transaction commit
/// 
/// Characteristics:
/// - Past tense naming (EmployeeCreated, UserCreated, DepartmentRenamed)
/// - Immutable (use record type)
/// - Saved to OutboxMessage table first (reliability)
/// - Published by background service (eventual consistency)
/// - Used for cross-module/cross-service communication
/// 
/// Outbox Pattern Flow:
/// 1. Domain operation executes (e.g., create Employee in Personnel module)
/// 2. Domain event handler converts to integration event
/// 3. Integration event serialized to JSON
/// 4. Saved to OutboxMessage table (same transaction as domain changes)
/// 5. Transaction commits (both Employee and OutboxMessage saved atomically)
/// 6. Background service picks up OutboxMessage
/// 7. Deserializes integration event
/// 8. Publishes via Event Bus (RabbitMQ, Azure Service Bus, or in-process)
/// 9. Marks OutboxMessage as processed
/// 10. Other modules' handlers receive and process
/// 
/// Examples:
/// 
/// Personnel → Identity:
/// - EmployeeCreatedIntegrationEvent → Auto-create User account
/// - EmployeeTerminatedIntegrationEvent → Deactivate User account
/// 
/// Identity → Personnel:
/// - UserScopeLevelChangedIntegrationEvent → Update employee permissions cache
/// 
/// Organization → Multiple modules:
/// - DepartmentCreatedIntegrationEvent → Update org chart, notify employees
/// 
/// MediatR Integration:
/// - Extends INotification so MediatR can dispatch to handlers
/// - Multiple modules can subscribe to same event
/// - Each module has its own handlers
/// - InProcessEventBus uses MediatR.Publish()
/// 
/// Idempotency:
/// - Events may be delivered multiple times (at-least-once delivery)
/// - Handlers MUST be idempotent (check if already processed)
/// - Use event.Id to track processed events
/// 
/// Serialization:
/// - Must be JSON serializable (System.Text.Json)
/// - Only include necessary data (keep payload small)
/// - Avoid circular references
/// - Use primitive types when possible
/// </summary>
public interface IIntegrationEvent : INotification
{
    /// <summary>
    /// Unique identifier of the integration event
    /// Critical for idempotency!
    /// 
    /// Usage:
    /// - Before processing event, check if event.Id already processed
    /// - Store processed event IDs in database or cache
    /// - Skip processing if already handled
    /// 
    /// Example:
    /// <code>
    /// if (await _processedEvents.ContainsAsync(event.Id))
    ///     return; // Already processed, skip
    /// 
    /// // Process event...
    /// 
    /// await _processedEvents.AddAsync(event.Id);
    /// </code>
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// When the integration event occurred (UTC)
    /// Represents when the original domain event happened
    /// 
    /// Important for:
    /// - Event ordering: Process events in chronological order
    /// - Debugging: Trace when event originated
    /// - Auditing: Compliance requirements
    /// - SLA tracking: Measure event processing delays
    /// 
    /// Note:
    /// - This is when domain event occurred, NOT when integration event published
    /// - Background service processes in OccurredOnUtc order
    /// - Ensures causality (effects happen after causes)
    /// </summary>
    DateTime OccurredOnUtc { get; }
}
