using HRM.BuildingBlocks.Domain.Events;

namespace HRM.BuildingBlocks.Application.Abstractions.EventBus;

/// <summary>
/// Event bus for publishing and subscribing to integration events.
/// Enables cross-module communication in a loosely coupled manner.
/// 
/// Integration Events vs Domain Events:
/// 
/// Domain Events:
/// - Occur within a single module
/// - Dispatched synchronously within transaction
/// - Used for consistency within bounded context
/// - Example: OperatorRegisteredDomainEvent
/// 
/// Integration Events:
/// - Communicate between modules
/// - Published asynchronously via event bus
/// - Used for eventual consistency across modules
/// - Example: OperatorRegisteredIntegrationEvent
/// 
/// Event Flow:
/// <code>
/// 1. Domain Event Raised:
///    Operator.Register() → OperatorRegisteredDomainEvent
/// 
/// 2. Domain Event Handler (synchronous, in transaction):
///    OperatorRegisteredDomainEventHandler
///    → Creates OutboxMessage with serialized integration event
///    → Commits OutboxMessage in same transaction
/// 
/// 3. Background Job (OutboxProcessor):
///    → Reads unprocessed OutboxMessages
///    → Deserializes integration event
///    → Publishes to event bus
///    → Marks OutboxMessage as processed
/// 
/// 4. Event Bus:
///    → Publishes event to subscribers
///    → In-memory (modular monolith) or RabbitMQ (microservices)
/// 
/// 5. Integration Event Handler (in other module):
///    → Receives event from bus
///    → Processes event (e.g., create User from Employee)
///    → May raise own domain events
/// </code>
/// 
/// Implementation Options:
/// 
/// 1. In-Memory Event Bus (Modular Monolith):
/// <code>
/// public class InMemoryEventBus : IEventBus
/// {
///     private readonly IMediator _mediator;
///     
///     public async Task PublishAsync&lt;T&gt;(T integrationEvent, ...) where T : IIntegrationEvent
///     {
///         // Use MediatR to publish to in-process handlers
///         await _mediator.Publish(integrationEvent, cancellationToken);
///     }
/// }
/// </code>
/// 
/// 2. RabbitMQ Event Bus (Microservices):
/// <code>
/// public class RabbitMqEventBus : IEventBus
/// {
///     private readonly IConnection _connection;
///     
///     public async Task PublishAsync&lt;T&gt;(T integrationEvent, ...) where T : IIntegrationEvent
///     {
///         var eventName = integrationEvent.GetType().Name;
///         var message = JsonSerializer.Serialize(integrationEvent);
///         
///         using var channel = _connection.CreateModel();
///         channel.ExchangeDeclare(exchange: "hrm_events", type: "topic");
///         
///         var body = Encoding.UTF8.GetBytes(message);
///         channel.BasicPublish(
///             exchange: "hrm_events",
///             routingKey: eventName,
///             basicProperties: null,
///             body: body
///         );
///     }
/// }
/// </code>
/// 
/// Module Communication Example:
/// <code>
/// // Identity Module publishes:
/// await _eventBus.PublishAsync(
///     new OperatorRegisteredIntegrationEvent
///     {
///         OperatorId = @operator.Id,
///         Username = @operator.Username,
///         Email = @operator.Email,
///         OccurredAt = DateTime.UtcNow
///     }
/// );
/// 
/// // Organization Module subscribes:
/// public class OperatorRegisteredIntegrationEventHandler
///     : IIntegrationEventHandler&lt;OperatorRegisteredIntegrationEvent&gt;
/// {
///     public async Task Handle(OperatorRegisteredIntegrationEvent @event, ...)
///     {
///         // Create default permissions for new operator
///         var permissions = CreateDefaultPermissions(@event.OperatorId);
///         await _repository.AddAsync(permissions);
///     }
/// }
/// </code>
/// 
/// Subscription Patterns:
/// 
/// 1. Multiple Handlers (Fan-Out):
/// <code>
/// // Event: EmployeeCreatedIntegrationEvent
/// 
/// // Handler 1: Identity Module - Create User account
/// // Handler 2: Payroll Module - Setup payroll record
/// // Handler 3: Benefits Module - Enroll in benefits
/// 
/// // All handlers receive same event independently
/// </code>
/// 
/// 2. Conditional Processing:
/// <code>
/// public async Task Handle(EmployeeCreatedIntegrationEvent @event, ...)
/// {
///     // Only process if certain conditions met
///     if (@event.EmployeeType == EmployeeType.FullTime)
///     {
///         await EnrollInBenefitsAsync(@event.EmployeeId);
///     }
/// }
/// </code>
/// 
/// Error Handling:
/// <code>
/// public async Task PublishAsync&lt;T&gt;(T integrationEvent, ...) where T : IIntegrationEvent
/// {
///     try
///     {
///         await _messageBroker.PublishAsync(integrationEvent);
///         _logger.LogInformation(
///             "Published integration event {EventType}",
///             typeof(T).Name
///         );
///     }
///     catch (Exception ex)
///     {
///         _logger.LogError(
///             ex,
///             "Failed to publish integration event {EventType}",
///             typeof(T).Name
///         );
///         
///         // Don't throw - event is already in outbox
///         // OutboxProcessor will retry later
///     }
/// }
/// </code>
/// 
/// Retry Strategy:
/// - Integration events stored in OutboxMessages
/// - Failed publishes retried by OutboxProcessor
/// - Exponential backoff for retries
/// - Dead letter queue for permanent failures
/// 
/// Idempotency:
/// Integration event handlers should be idempotent:
/// <code>
/// public async Task Handle(EmployeeCreatedIntegrationEvent @event, ...)
/// {
///     // Check if already processed
///     if (await _repository.UserExistsByEmployeeIdAsync(@event.EmployeeId))
///     {
///         _logger.LogInformation(
///             "User already exists for employee {EmployeeId}, skipping",
///             @event.EmployeeId
///         );
///         return;
///     }
///     
///     // Create user
///     var user = User.Create(...);
///     await _repository.AddAsync(user);
/// }
/// </code>
/// 
/// Testing:
/// <code>
/// public class InMemoryEventBusTests
/// {
///     [Fact]
///     public async Task PublishAsync_ShouldDispatchToAllHandlers()
///     {
///         // Arrange
///         var handler1 = new Mock&lt;IIntegrationEventHandler&lt;TestEvent&gt;&gt;();
///         var handler2 = new Mock&lt;IIntegrationEventHandler&lt;TestEvent&gt;&gt;();
///         var eventBus = CreateEventBus(handler1.Object, handler2.Object);
///         
///         // Act
///         await eventBus.PublishAsync(new TestEvent());
///         
///         // Assert
///         handler1.Verify(h => h.Handle(It.IsAny&lt;TestEvent&gt;(), ...), Times.Once);
///         handler2.Verify(h => h.Handle(It.IsAny&lt;TestEvent&gt;(), ...), Times.Once);
///     }
/// }
/// </code>
/// 
/// Migration Path:
/// - Start with in-memory event bus (modular monolith)
/// - Same interface works for both implementations
/// - Switch to RabbitMQ when ready for microservices
/// - No code changes in modules (only DI registration)
/// 
/// Performance Considerations:
/// - In-memory: Fast, no network overhead
/// - RabbitMQ: Network latency, message serialization
/// - Asynchronous processing doesn't block request
/// - Eventual consistency acceptable for cross-module operations
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Publishes an integration event to all subscribers.
    /// Used for cross-module communication via event bus.
    /// 
    /// Publishing Flow:
    /// 1. Serialize event to JSON
    /// 2. Send to message broker (in-memory or RabbitMQ)
    /// 3. Message broker dispatches to all subscribers
    /// 4. Each subscriber processes event independently
    /// 
    /// Delivery Guarantees:
    /// - At-least-once delivery (event may be delivered multiple times)
    /// - Handlers must be idempotent
    /// - No ordering guarantee between different event types
    /// - Ordering preserved for same event type (usually)
    /// 
    /// Error Handling:
    /// - Failures logged but don't throw exceptions
    /// - OutboxProcessor will retry failed publishes
    /// - Dead letter queue for permanent failures
    /// 
    /// Usage (from OutboxProcessor):
    /// <code>
    /// // Deserialize integration event from OutboxMessage
    /// var integrationEvent = JsonSerializer.Deserialize&lt;EmployeeCreatedIntegrationEvent&gt;(
    ///     outboxMessage.Content
    /// );
    /// 
    /// // Publish to event bus
    /// await _eventBus.PublishAsync(integrationEvent, cancellationToken);
    /// 
    /// // Mark outbox message as processed
    /// outboxMessage.MarkAsProcessed();
    /// await _unitOfWork.CommitAsync(cancellationToken);
    /// </code>
    /// 
    /// Thread Safety:
    /// - Implementation must be thread-safe
    /// - Multiple OutboxProcessor instances may publish concurrently
    /// - Typically registered as singleton
    /// </summary>
    /// <typeparam name="T">Type of integration event to publish</typeparam>
    /// <param name="integrationEvent">Event instance to publish</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task PublishAsync<T>(T integrationEvent, CancellationToken cancellationToken = default)
        where T : IIntegrationEvent;
}
