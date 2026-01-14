using HRM.BuildingBlocks.Application.Abstractions.Commands;
using HRM.BuildingBlocks.Domain.Abstractions.UnitOfWork;
using MediatR;

namespace HRM.BuildingBlocks.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior for automatically committing database transactions.
/// Commits changes for successful commands using Unit of Work pattern.
/// 
/// Pipeline Position (Last behavior before handler returns):
/// <code>
/// 1. LoggingBehavior (logs request)
/// 2. ValidationBehavior (validates)
/// 3. CommandHandler (executes business logic)
/// 4. UnitOfWorkBehavior ← auto-commits if success
/// 5. LoggingBehavior (logs result)
/// </code>
/// 
/// Applied To:
/// - Commands only (implement ICommandBase)
/// - NOT queries (queries are read-only, no commit needed)
/// 
/// Commit Strategy:
/// <code>
/// IF request is command AND result.IsSuccess:
///   1. Collect domain events from tracked entities
///   2. Dispatch domain events synchronously
///   3. Save all changes (entities + outbox messages) atomically
///   4. Return original result
/// 
/// IF request is command AND result.IsFailure:
///   - No commit (EF Core automatically discards changes)
///   - Return failure result
/// 
/// IF request is query:
///   - Skip entirely (queries don't modify state)
///   - Return query result directly
/// </code>
/// 
/// Why Automatic Commit:
/// 
/// 1. Consistency:
/// - Handlers don't need to remember to call CommitAsync
/// - Centralized transaction management
/// - Reduces boilerplate code
/// 
/// 2. Correctness:
/// - Only commits on success
/// - Failures automatically rolled back
/// - Domain events dispatched within transaction
/// 
/// 3. Simplicity:
/// - Handlers focus on business logic
/// - Infrastructure concerns handled by pipeline
/// - Easy to understand and maintain
/// 
/// Handler Without UnitOfWorkBehavior (Manual):
/// <code>
/// public async Task&lt;Result&lt;Guid&gt;&gt; Handle(
///     RegisterOperatorCommand command,
///     CancellationToken cancellationToken)
/// {
///     // Business logic
///     var @operator = Operator.Register(...);
///     await _repository.AddAsync(@operator);
///     
///     // ❌ Handler must remember to commit
///     await _unitOfWork.CommitAsync(cancellationToken);
///     
///     return Result.Success(@operator.Id);
/// }
/// </code>
/// 
/// Handler With UnitOfWorkBehavior (Automatic):
/// <code>
/// public async Task&lt;Result&lt;Guid&gt;&gt; Handle(
///     RegisterOperatorCommand command,
///     CancellationToken cancellationToken)
/// {
///     // Business logic only
///     var @operator = Operator.Register(...);
///     await _repository.AddAsync(@operator);
///     
///     // ✅ No manual commit - UnitOfWorkBehavior handles it
///     return Result.Success(@operator.Id);
/// }
/// // UnitOfWorkBehavior automatically calls CommitAsync after this returns
/// </code>
/// 
/// Domain Events Flow:
/// <code>
/// // 1. Handler raises domain event
/// var @operator = Operator.Register(...);
/// // Internal: @operator._domainEvents.Add(new OperatorRegisteredDomainEvent(...))
/// 
/// await _repository.AddAsync(@operator);
/// return Result.Success(@operator.Id);
/// 
/// // 2. UnitOfWorkBehavior sees Result.IsSuccess
/// // 3. Calls _unitOfWork.CommitAsync()
/// // 4. UnitOfWork implementation:
/// //    a. Collects domain events from @operator
/// //    b. Dispatches OperatorRegisteredDomainEvent
/// //    c. Handler creates OutboxMessage
/// //    d. Saves @operator + OutboxMessage atomically
/// //    e. Clears domain events from @operator
/// </code>
/// 
/// Transaction Scope:
/// <code>
/// // Each command has its own transaction
/// Request 1: RegisterOperatorCommand
///   → Transaction 1: Register operator
///   → Commit
/// 
/// Request 2: AssignRoleCommand
///   → Transaction 2: Assign role
///   → Commit
/// 
/// // Transactions are isolated, no cross-request transactions
/// </code>
/// 
/// Failure Scenarios:
/// 
/// 1. Validation Failure:
/// <code>
/// // ValidationBehavior returns Result.Failure
/// // UnitOfWorkBehavior sees IsFailure = true
/// // No commit, EF Core discards changes automatically
/// </code>
/// 
/// 2. Business Rule Violation:
/// <code>
/// public async Task&lt;Result&lt;Guid&gt;&gt; Handle(...)
/// {
///     if (await _repository.ExistsByUsernameAsync(command.Username))
///         return Result.Failure(Error.Conflict(...));
///     
///     // Result.IsFailure, no commit
/// }
/// </code>
/// 
/// 3. Database Exception:
/// <code>
/// // Handler returns Result.Success
/// // UnitOfWorkBehavior calls CommitAsync
/// // SaveChanges throws DbUpdateException (e.g., unique constraint)
/// // Exception propagates up
/// // Transaction automatically rolled back by EF Core
/// // Global exception handler catches and returns 500
/// </code>
/// 
/// 4. Domain Event Handler Exception:
/// <code>
/// // SaveChanges dispatches domain events
/// // Domain event handler throws exception
/// // Exception propagates up from SaveChanges
/// // Transaction rolled back
/// // Entity + OutboxMessage not saved
/// </code>
/// 
/// Multiple Handlers per Command:
/// Commands should have only ONE handler (MediatR enforces this).
/// But domain event handlers can be multiple:
/// 
/// <code>
/// // One command handler:
/// RegisterOperatorCommandHandler → raises OperatorRegisteredDomainEvent
/// 
/// // Multiple domain event handlers (executed in SaveChanges):
/// Handler 1: CreateOutboxMessageHandler → creates OutboxMessage
/// Handler 2: LogAuditTrailHandler → logs to audit table
/// Handler 3: SendNotificationHandler → queues notification
/// 
/// // All committed atomically in UnitOfWork.CommitAsync
/// </code>
/// 
/// Testing:
/// <code>
/// public class UnitOfWorkBehaviorTests
/// {
///     [Fact]
///     public async Task Handle_WhenCommandSucceeds_ShouldCommit()
///     {
///         // Arrange
///         var unitOfWork = new Mock&lt;IUnitOfWork&gt;();
///         var behavior = new UnitOfWorkBehavior&lt;TestCommand, Result&gt;(unitOfWork.Object);
///         
///         // Act
///         await behavior.Handle(
///             new TestCommand(),
///             () => Task.FromResult(Result.Success()),
///             CancellationToken.None
///         );
///         
///         // Assert
///         unitOfWork.Verify(u => u.CommitAsync(It.IsAny&lt;CancellationToken&gt;()), Times.Once);
///     }
///     
///     [Fact]
///     public async Task Handle_WhenCommandFails_ShouldNotCommit()
///     {
///         // Arrange
///         var unitOfWork = new Mock&lt;IUnitOfWork&gt;();
///         var behavior = new UnitOfWorkBehavior&lt;TestCommand, Result&gt;(unitOfWork.Object);
///         
///         // Act
///         await behavior.Handle(
///             new TestCommand(),
///             () => Task.FromResult(Result.Failure(Error.Validation(...))),
///             CancellationToken.None
///         );
///         
///         // Assert
///         unitOfWork.Verify(u => u.CommitAsync(It.IsAny&lt;CancellationToken&gt;()), Times.Never);
///     }
///     
///     [Fact]
///     public async Task Handle_WhenQuery_ShouldNotCommit()
///     {
///         // Arrange
///         var unitOfWork = new Mock&lt;IUnitOfWork&gt;();
///         var behavior = new UnitOfWorkBehavior&lt;TestQuery, TestDto&gt;(unitOfWork.Object);
///         
///         // Act
///         await behavior.Handle(
///             new TestQuery(),
///             () => Task.FromResult(new TestDto()),
///             CancellationToken.None
///         );
///         
///         // Assert
///         unitOfWork.Verify(u => u.CommitAsync(It.IsAny&lt;CancellationToken&gt;()), Times.Never);
///     }
/// }
/// </code>
/// 
/// Performance Considerations:
/// - Commit is relatively expensive (database round-trip)
/// - Domain event dispatching adds overhead
/// - Keep transactions short (avoid long-running operations)
/// - Consider async handlers for domain events (but they must complete in transaction)
/// 
/// Best Practices:
/// 
/// 1. Return Early on Failure:
/// <code>
/// public async Task&lt;Result&gt; Handle(...)
/// {
///     // Validate early
///     if (invalid)
///         return Result.Failure(...); // No commit
///     
///     // Business logic
///     await DoWorkAsync();
///     
///     return Result.Success(); // Commit happens here
/// }
/// </code>
/// 
/// 2. Don't Call CommitAsync in Handlers:
/// <code>
/// // ❌ BAD
/// public async Task&lt;Result&gt; Handle(...)
/// {
///     await _repository.AddAsync(entity);
///     await _unitOfWork.CommitAsync(); // Don't do this!
///     return Result.Success();
/// }
/// 
/// // ✅ GOOD
/// public async Task&lt;Result&gt; Handle(...)
/// {
///     await _repository.AddAsync(entity);
///     return Result.Success(); // UnitOfWorkBehavior commits
/// }
/// </code>
/// 
/// 3. Use Result Pattern Correctly:
/// <code>
/// // ❌ BAD - Throwing exceptions for business failures
/// public async Task&lt;Result&gt; Handle(...)
/// {
///     if (invalid)
///         throw new ValidationException(); // Don't throw!
/// }
/// 
/// // ✅ GOOD - Return Result.Failure
/// public async Task&lt;Result&gt; Handle(...)
/// {
///     if (invalid)
///         return Result.Failure(Error.Validation(...));
/// }
/// </code>
/// </summary>
/// <typeparam name="TRequest">Type of request (must be command)</typeparam>
/// <typeparam name="TResponse">Type of response (must be Result or Result&lt;T&gt;)</typeparam>
public sealed class UnitOfWorkBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IUnitOfWork _unitOfWork;

    public UnitOfWorkBehavior(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Handles automatic transaction commit for successful commands.
    /// Only commits when request is command and result indicates success.
    /// </summary>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Only commit for commands (ICommandBase)
        if (request is not ICommandBase)
        {
            // Not a command (probably a query), skip commit
            return await next();
        }

        // Execute handler (command logic)
        var response = await next();

        // Check if result indicates success
        if (IsSuccessResult(response))
        {
            // Commit transaction (includes domain event dispatching)
            await _unitOfWork.CommitAsync(cancellationToken);
        }

        // Return original response (success or failure)
        return response;
    }

    /// <summary>
    /// Checks if response indicates successful operation.
    /// </summary>
    private static bool IsSuccessResult(TResponse response)
    {
        // Check if response is Result or Result<T>
        return response switch
        {
            Results.Result result => result.IsSuccess,
            _ => false
        };
    }
}
