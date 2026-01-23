using HRM.BuildingBlocks.Application.Abstractions.EventBus;
using HRM.Modules.Identity.Domain.Events;
using HRM.Modules.Identity.IntegrationEvents;
using MediatR;

namespace HRM.Modules.Identity.Infrastructure.DomainEventHandlers;

/// <summary>
/// Domain event handler for OperatorRegisteredDomainEvent.
/// Creates integration event for cross-module communication.
///
/// LOCATION: Infrastructure Layer
/// Moved from Application to Infrastructure to fix dependency violation:
/// - Application should NOT depend on IntegrationEvents
/// - Infrastructure CAN depend on both Application and IntegrationEvents
/// - This handler is infrastructure concern (outbox message creation)
///
/// Domain Events vs Integration Events:
/// - Domain Event: In-process, same transaction, synchronous
///   * Raised by: Operator.Register()
///   * Processed by: This handler
///   * Purpose: Domain logic side effects
///
/// - Integration Event: Cross-process, async, eventually consistent
///   * Created by: This handler (stored in OutboxMessages)
///   * Published by: OutboxProcessor background service
///   * Consumed by: Other modules (Personnel, Notifications, etc.)
///   * Purpose: Cross-module communication
///
/// Responsibilities:
/// 1. Create OperatorRegisteredIntegrationEvent
/// 2. Publish via IEventBus (stores in OutboxMessages table)
/// 3. Log the event (optional)
///
/// Transaction Guarantee:
/// - This handler runs in same transaction as RegisterOperator command
/// - Integration event stored in OutboxMessages table (same transaction)
/// - If transaction fails: No integration event published
/// - If transaction succeeds: OutboxProcessor will publish event
///
/// Idempotency:
/// - OutboxProcessor ensures each integration event published exactly once
/// - Consumers should be idempotent (handle duplicate events)
///
/// Example Use Cases for Integration Event:
/// - Personnel module: Create corresponding Employee record
/// - Notifications module: Send welcome email
/// - Audit module: Log operator registration
/// - Analytics module: Track user growth metrics
///
/// Performance:
/// - Runs synchronously within RegisterOperator transaction
/// - 1 INSERT into OutboxMessages table (~5ms)
/// - Actual publishing happens asynchronously (OutboxProcessor)
/// </summary>
internal sealed class OperatorRegisteredDomainEventHandler
    : INotificationHandler<OperatorRegisteredDomainEvent>
{
    private readonly IEventBus _eventBus;

    public OperatorRegisteredDomainEventHandler(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public async Task Handle(OperatorRegisteredDomainEvent notification, CancellationToken cancellationToken)
    {
        // Create integration event
        // This event will be consumed by other modules
        var integrationEvent = new OperatorRegisteredIntegrationEvent(
            Id: Guid.NewGuid(),
            OccurredOnUtc: DateTime.UtcNow,
            OperatorId: notification.OperatorId,
            Username: notification.Username,
            Email: notification.Email,
            FullName: notification.FullName
        );

        // Publish integration event
        // IEventBus implementation (InMemoryEventBus) will:
        // 1. Store event in OutboxMessages table (same transaction)
        // 2. OutboxProcessor will publish it asynchronously
        await _eventBus.PublishAsync(integrationEvent, cancellationToken);

        // Note: No explicit logging here
        // LoggingBehavior already logs command execution
        // OutboxProcessor logs event publishing
    }
}
