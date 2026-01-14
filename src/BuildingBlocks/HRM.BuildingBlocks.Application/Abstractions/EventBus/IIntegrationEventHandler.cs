using HRM.BuildingBlocks.Domain.Abstractions.Events;
using MediatR;

namespace HRM.BuildingBlocks.Application.Abstractions.EventBus;

/// <summary>
/// Handler interface for processing integration events from other modules.
/// Integration event handlers enable cross-module communication in a loosely coupled manner.
/// 
/// Handler Responsibilities:
/// 1. Receive integration events from event bus
/// 2. Process event in local module context
/// 3. May raise domain events (which create new outbox messages)
/// 4. Should be idempotent (handle duplicate events gracefully)
/// 5. Should not throw exceptions (log and continue)
/// 
/// Integration vs Domain Event Handlers:
/// 
/// Domain Event Handler:
/// - Handles events within same module
/// - Executes synchronously within transaction
/// - Creates OutboxMessages for integration events
/// - Example: OperatorRegisteredDomainEventHandler
/// 
/// Integration Event Handler:
/// - Handles events from other modules
/// - Executes asynchronously via event bus
/// - Processes in own module's context
/// - Example: EmployeeCreatedIntegrationEventHandler (in Identity module)
/// 
/// Event Flow Example:
/// <code>
/// MODULE: Personnel
/// ================
/// 1. Employee.Create() → EmployeeCreatedDomainEvent raised
/// 2. EmployeeCreatedDomainEventHandler:
///    - Maps to EmployeeCreatedIntegrationEvent
///    - Creates OutboxMessage
///    - Commits in transaction
/// 3. OutboxProcessor (background job):
///    - Reads OutboxMessage
///    - Publishes EmployeeCreatedIntegrationEvent to event bus
/// 
/// MODULE: Identity
/// ===============
/// 4. EmployeeCreatedIntegrationEventHandler receives event
/// 5. Auto-creates User account:
///    - Generate username from employee name
///    - Generate temporary password
///    - Determine scope level from position
///    - Create User entity
///    - Send welcome email
/// </code>
/// 
/// Idempotency Pattern:
/// Integration events may be delivered multiple times. Handlers must be idempotent.
/// 
/// <code>
/// public class EmployeeCreatedIntegrationEventHandler 
///     : IIntegrationEventHandler&lt;EmployeeCreatedIntegrationEvent&gt;
/// {
///     private readonly IUserRepository _userRepository;
///     private readonly IUnitOfWork _unitOfWork;
///     
///     public async Task Handle(
///         EmployeeCreatedIntegrationEvent @event,
///         CancellationToken cancellationToken)
///     {
///         // Check if already processed (idempotency)
///         var existingUser = await _userRepository.GetByEmployeeIdAsync(@event.EmployeeId);
///         if (existingUser is not null)
///         {
///             _logger.LogInformation(
///                 "User already exists for employee {EmployeeId}, skipping duplicate event",
///                 @event.EmployeeId
///             );
///             return; // Graceful handling of duplicate
///         }
///         
///         // Process event (create user)
///         var user = User.Create(
///             @event.EmployeeId,
///             GenerateUsername(@event.FirstName, @event.LastName),
///             @event.Email,
///             GenerateTemporaryPassword(),
///             DetermineScopeLevel(@event.PositionId)
///         );
///         
///         await _userRepository.AddAsync(user);
///         await _unitOfWork.CommitAsync(cancellationToken);
///         
///         // Send welcome email (separate concern)
///         await _emailService.SendWelcomeEmailAsync(user, temporaryPassword);
///     }
/// }
/// </code>
/// 
/// Error Handling:
/// Handlers should NOT throw exceptions (would break event processing).
/// 
/// <code>
/// public async Task Handle(
///     EmployeeCreatedIntegrationEvent @event,
///     CancellationToken cancellationToken)
/// {
///     try
///     {
///         // Process event
///         await ProcessEventAsync(@event, cancellationToken);
///         
///         _logger.LogInformation(
///             "Successfully processed {EventType} for EmployeeId={EmployeeId}",
///             nameof(EmployeeCreatedIntegrationEvent),
///             @event.EmployeeId
///         );
///     }
///     catch (Exception ex)
///     {
///         _logger.LogError(
///             ex,
///             "Failed to process {EventType} for EmployeeId={EmployeeId}",
///             nameof(EmployeeCreatedIntegrationEvent),
///             @event.EmployeeId
///         );
///         
///         // Don't throw - log and continue
///         // Event bus will continue processing other handlers
///         // Consider dead letter queue for permanent failures
///     }
/// }
/// </code>
/// 
/// Multiple Handlers (Fan-Out):
/// Multiple modules can subscribe to same event.
/// 
/// <code>
/// // Event: EmployeeCreatedIntegrationEvent
/// 
/// // Handler 1 (Identity Module):
/// public class CreateUserFromEmployeeHandler 
///     : IIntegrationEventHandler&lt;EmployeeCreatedIntegrationEvent&gt;
/// {
///     public async Task Handle(...) 
///     {
///         // Create User account
///     }
/// }
/// 
/// // Handler 2 (Payroll Module):
/// public class SetupPayrollRecordHandler 
///     : IIntegrationEventHandler&lt;EmployeeCreatedIntegrationEvent&gt;
/// {
///     public async Task Handle(...) 
///     {
///         // Setup payroll record
///     }
/// }
/// 
/// // Handler 3 (Benefits Module):
/// public class EnrollInBenefitsHandler 
///     : IIntegrationEventHandler&lt;EmployeeCreatedIntegrationEvent&gt;
/// {
///     public async Task Handle(...) 
///     {
///         // Enroll in benefits program
///     }
/// }
/// 
/// // All handlers execute independently
/// </code>
/// 
/// Conditional Processing:
/// Handlers can implement conditional logic based on event data.
/// 
/// <code>
/// public async Task Handle(
///     EmployeeAssignmentChangedIntegrationEvent @event,
///     CancellationToken cancellationToken)
/// {
///     // Only process if scope-affecting change
///     bool scopeChanged = @event.OldDepartmentId != @event.NewDepartmentId;
///     
///     if (!scopeChanged)
///     {
///         _logger.LogDebug("Department unchanged, no action needed");
///         return;
///     }
///     
///     // Revoke sessions to force re-login with new scope
///     var user = await _userRepository.GetByEmployeeIdAsync(@event.EmployeeId);
///     if (user is not null)
///     {
///         await _refreshTokenRepository.RevokeAllUserTokensAsync(user.Id, "Department changed");
///         await _unitOfWork.CommitAsync(cancellationToken);
///     }
/// }
/// </code>
/// 
/// Testing:
/// <code>
/// public class EmployeeCreatedIntegrationEventHandlerTests
/// {
///     [Fact]
///     public async Task Handle_WhenNewEmployee_ShouldCreateUser()
///     {
///         // Arrange
///         var @event = new EmployeeCreatedIntegrationEvent
///         {
///             EmployeeId = Guid.NewGuid(),
///             FirstName = "John",
///             LastName = "Doe",
///             Email = "john.doe@example.com",
///             OccurredAt = DateTime.UtcNow
///         };
///         
///         var handler = CreateHandler();
///         
///         // Act
///         await handler.Handle(@event, CancellationToken.None);
///         
///         // Assert
///         var user = await _userRepository.GetByEmployeeIdAsync(@event.EmployeeId);
///         user.Should().NotBeNull();
///         user.GetEmail().Should().Be(@event.Email);
///     }
///     
///     [Fact]
///     public async Task Handle_WhenDuplicateEvent_ShouldBeIdempotent()
///     {
///         // Arrange
///         var @event = new EmployeeCreatedIntegrationEvent { ... };
///         var handler = CreateHandler();
///         
///         // Act - Process event twice
///         await handler.Handle(@event, CancellationToken.None);
///         await handler.Handle(@event, CancellationToken.None);
///         
///         // Assert - Only one user created
///         var users = await _userRepository.GetAllByEmployeeIdAsync(@event.EmployeeId);
///         users.Should().HaveCount(1);
///     }
/// }
/// </code>
/// 
/// Registration in DI:
/// <code>
/// // Module startup
/// services.AddMediatR(cfg =>
/// {
///     // Register all integration event handlers in assembly
///     cfg.RegisterServicesFromAssembly(typeof(EmployeeCreatedIntegrationEventHandler).Assembly);
/// });
/// </code>
/// 
/// Event Versioning:
/// Handlers should handle different event versions gracefully.
/// 
/// <code>
/// public async Task Handle(
///     EmployeeCreatedIntegrationEvent @event,
///     CancellationToken cancellationToken)
/// {
///     // Handle v1 events (no PhoneNumber)
///     if (@event.Version == 1)
///     {
///         var user = User.Create(
///             @event.EmployeeId,
///             @event.Email,
///             GenerateUsername(@event.FirstName, @event.LastName),
///             phoneNumber: null // Not available in v1
///         );
///     }
///     
///     // Handle v2 events (with PhoneNumber)
///     if (@event.Version >= 2)
///     {
///         var user = User.Create(
///             @event.EmployeeId,
///             @event.Email,
///             GenerateUsername(@event.FirstName, @event.LastName),
///             phoneNumber: @event.PhoneNumber // Available in v2
///         );
///     }
/// }
/// </code>
/// 
/// Performance Considerations:
/// - Handlers execute asynchronously (don't block request)
/// - Keep processing fast (< 1 second ideal)
/// - For long operations, consider raising new event for background job
/// - Don't call external APIs synchronously (use outbox for that)
/// </summary>
/// <typeparam name="TIntegrationEvent">Type of integration event to handle</typeparam>
public interface IIntegrationEventHandler<in TIntegrationEvent>
    : INotificationHandler<TIntegrationEvent>
    where TIntegrationEvent : IIntegrationEvent
{
    // MediatR INotificationHandler<T> provides:
    // Task Handle(TIntegrationEvent notification, CancellationToken cancellationToken);
}
