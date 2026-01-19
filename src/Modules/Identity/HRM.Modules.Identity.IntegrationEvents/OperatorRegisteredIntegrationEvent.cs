using HRM.BuildingBlocks.Domain.Abstractions.Events;

namespace HRM.Modules.Identity.IntegrationEvents;

/// <summary>
/// Integration event raised when an operator is registered
/// Published asynchronously via Transactional Outbox pattern
///
/// Integration Events vs Domain Events:
/// - Domain Event: In-process, same transaction, synchronous
///   * Example: OperatorRegisteredDomainEvent
///   * Processed by: Domain event handlers in same module
///   * Purpose: Domain logic side effects (create integration event)
///
/// - Integration Event: Cross-process, async, eventually consistent
///   * Example: This event
///   * Stored in: OutboxMessages table (same transaction)
///   * Published by: OutboxProcessor background service
///   * Consumed by: Other modules (Personnel, Notifications, etc.)
///   * Purpose: Cross-module communication
///
/// Event Flow:
/// 1. RegisterOperatorCommand executed
/// 2. Operator.Register() raises OperatorRegisteredDomainEvent
/// 3. OperatorRegisteredDomainEventHandler creates this integration event
/// 4. Event stored in Identity.OutboxMessages table (same transaction)
/// 5. IdentityOutboxProcessor publishes event to event bus (60s interval)
/// 6. Other modules consume event via their event handlers
///
/// Consumers (Example Use Cases):
/// - Personnel Module:
///   * Subscribe to this event
///   * Create corresponding Employee record
///   * Link operator ID to employee ID
///
/// - Notifications Module:
///   * Subscribe to this event
///   * Send welcome email to operator
///   * Include activation instructions
///
/// - Audit Module:
///   * Subscribe to this event
///   * Log operator registration for audit trail
///   * Track who registered the operator
///
/// - Analytics Module:
///   * Subscribe to this event
///   * Track user growth metrics
///   * Generate reports
///
/// Serialization:
/// - Stored as JSON in OutboxMessages.Content column
/// - Uses System.Text.Json for serialization
/// - All properties must be JSON-serializable
/// - Use record type for immutability
///
/// Versioning:
/// - Version: Implicit (Type property contains full type name with version)
/// - Breaking changes: Create new event type (OperatorRegisteredIntegrationEventV2)
/// - Backward compatibility: Keep old event handlers running during migration
///
/// Idempotency:
/// - Consumers MUST be idempotent (handle duplicate events)
/// - OutboxProcessor ensures at-least-once delivery
/// - Use event ID for deduplication in consumers
///
/// Retry Policy:
/// - Max attempts: 5 (configurable in OutboxProcessor)
/// - Failed events marked as dead letter after max attempts
/// - Manual intervention required for dead letter events
/// </summary>
/// <param name="Id">Unique event ID (for deduplication)</param>
/// <param name="OccurredOnUtc">Timestamp when event occurred (UTC)</param>
/// <param name="OperatorId">ID of registered operator</param>
/// <param name="Username">Username of registered operator</param>
/// <param name="Email">Email of registered operator</param>
/// <param name="FullName">Full name of registered operator</param>
public sealed record OperatorRegisteredIntegrationEvent(
    Guid Id,
    DateTime OccurredOnUtc,
    Guid OperatorId,
    string Username,
    string Email,
    string FullName
) : IIntegrationEvent;
