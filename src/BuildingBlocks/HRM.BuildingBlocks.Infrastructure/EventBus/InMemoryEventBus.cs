using HRM.BuildingBlocks.Application.Abstractions.EventBus;
using HRM.BuildingBlocks.Domain.Abstractions.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HRM.BuildingBlocks.Infrastructure.EventBus;

/// <summary>
/// In-memory event bus implementation using MediatR
/// Suitable for modular monolith architecture
///
/// How It Works:
/// - Uses MediatR to publish integration events in-process
/// - All modules within same process can subscribe to events
/// - Events dispatched synchronously to all handlers
/// - Fast, no network overhead, no message serialization
///
/// Benefits:
/// - Simple implementation (just MediatR wrapper)
/// - No infrastructure dependencies (no RabbitMQ, no Redis)
/// - Fast (in-process, no network latency)
/// - Easy to debug (all in same process)
/// - Perfect for modular monolith
///
/// Limitations:
/// - Only works within single process (not for microservices)
/// - No persistence (events lost if process crashes before handling)
/// - No scalability across multiple instances
/// - No retry mechanism (handled by OutboxProcessor instead)
///
/// Migration Path:
/// When ready for microservices, swap with RabbitMqEventBus:
/// - Same IEventBus interface
/// - Only change DI registration
/// - No code changes in modules
///
/// Event Flow:
/// 1. OutboxProcessor reads OutboxMessage
/// 2. Deserializes integration event
/// 3. Calls PublishAsync() on this bus
/// 4. MediatR dispatches to all registered INotificationHandler<T>
/// 5. All handlers receive event and process independently
///
/// Handler Registration Example:
/// <code>
/// // In Personnel Module
/// public class OperatorRegisteredIntegrationEventHandler
///     : INotificationHandler<OperatorRegisteredIntegrationEvent>
/// {
///     public async Task Handle(OperatorRegisteredIntegrationEvent evt, CancellationToken ct)
///     {
///         // Handle event (e.g., create user profile)
///         _logger.LogInformation(
///             "Received OperatorRegisteredIntegrationEvent for {Username}",
///             evt.Username
///         );
///     }
/// }
///
/// // DI Registration
/// services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(PersonnelModule).Assembly));
/// </code>
///
/// Threading:
/// - Thread-safe (MediatR handles concurrency)
/// - Multiple OutboxProcessor instances can publish concurrently
/// - Handlers execute sequentially per event (MediatR default)
///
/// Error Handling:
/// - Logs errors but doesn't throw
/// - OutboxProcessor will retry on failure
/// - Failed events remain in outbox for retry
/// </summary>
public sealed class InMemoryEventBus : IEventBus
{
    private readonly IPublisher _publisher;
    private readonly ILogger<InMemoryEventBus> _logger;

    /// <summary>
    /// Constructor with MediatR publisher and logger
    /// </summary>
    /// <param name="publisher">MediatR publisher for dispatching events</param>
    /// <param name="logger">Logger for tracking event publishing</param>
    public InMemoryEventBus(IPublisher publisher, ILogger<InMemoryEventBus> logger)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Publish integration event to all in-process subscribers
    ///
    /// Flow:
    /// 1. Log event publishing attempt
    /// 2. Use MediatR to publish event (INotification)
    /// 3. MediatR dispatches to all registered handlers
    /// 4. Log success
    /// 5. If error, log but don't throw (OutboxProcessor will retry)
    ///
    /// Thread Safety:
    /// - MediatR publisher is thread-safe
    /// - Multiple threads can call PublishAsync concurrently
    /// - Handlers execute sequentially per event
    ///
    /// Performance:
    /// - In-process, no serialization, no network
    /// - Fast (~microseconds for dispatch)
    /// - Handlers may be slower (depends on logic)
    /// </summary>
    public async Task PublishAsync<T>(T integrationEvent, CancellationToken cancellationToken = default)
        where T : IIntegrationEvent
    {
        if (integrationEvent is null)
        {
            throw new ArgumentNullException(nameof(integrationEvent));
        }

        var eventName = typeof(T).Name;

        try
        {
            _logger.LogInformation(
                "Publishing integration event {EventName} (Id: {EventId})",
                eventName,
                integrationEvent.Id
            );

            // MediatR publishes to all registered INotificationHandler<T>
            await _publisher.Publish(integrationEvent, cancellationToken);

            _logger.LogInformation(
                "Successfully published integration event {EventName} (Id: {EventId})",
                eventName,
                integrationEvent.Id
            );
        }
        catch (Exception ex)
        {
            // Log error but don't throw
            // OutboxProcessor will retry the event later
            _logger.LogError(
                ex,
                "Failed to publish integration event {EventName} (Id: {EventId}). " +
                "Event will be retried by OutboxProcessor.",
                eventName,
                integrationEvent.Id
            );

            // Re-throw to let OutboxProcessor mark as failed and retry
            throw;
        }
    }
}
