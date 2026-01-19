using HRM.BuildingBlocks.Domain.Abstractions.Results;
using MediatR;

namespace HRM.BuildingBlocks.Application.Abstractions.Commands;

/// <summary>
/// Interface for command handlers that don't return data.
/// Implements business logic for commands and returns Result.
/// 
/// Handler Responsibilities:
/// 1. Load required entities from repositories
/// 2. Validate business rules (domain-level, not input validation)
/// 3. Execute domain logic by calling entity methods
/// 4. Persist changes via repositories
/// 5. Return Result.Success() or Result.Failure(error)
/// 
/// Handler Must NOT:
/// - Commit changes manually (UnitOfWorkBehavior does this)
/// - Dispatch domain events manually (UnitOfWork does this)
/// - Throw exceptions for business failures (return Result.Failure instead)
/// - Access infrastructure directly (use repositories)
/// 
/// Pipeline Execution Order:
/// 1. LoggingBehavior (logs request start)
/// 2. ValidationBehavior (FluentValidation - input validation)
/// 3. → CommandHandler (business logic) ← YOU ARE HERE
/// 4. UnitOfWorkBehavior (auto-commit if Result.IsSuccess)
/// 5. LoggingBehavior (logs result)
/// 
/// Transaction Management:
/// - Handler does NOT call CommitAsync
/// - UnitOfWorkBehavior commits automatically if Result.IsSuccess
/// - Automatic rollback if Result.IsFailure
/// - Domain events dispatched during commit
/// - OutboxMessages created in same transaction
/// 
/// Example Implementation:
/// <code>
/// public sealed class DeleteOperatorCommandHandler 
///     : ICommandHandler&lt;DeleteOperatorCommand&gt;
/// {
///     private readonly IOperatorRepository _repository;
///     private readonly ILogger&lt;DeleteOperatorCommandHandler&gt; _logger;
///     
///     public DeleteOperatorCommandHandler(
///         IOperatorRepository repository,
///         ILogger&lt;DeleteOperatorCommandHandler&gt; logger)
///     {
///         _repository = repository;
///         _logger = logger;
///     }
///     
///     public async Task&lt;Result&gt; Handle(
///         DeleteOperatorCommand command,
///         CancellationToken cancellationToken)
///     {
///         // 1. Load entity from repository
///         var @operator = await _repository.GetByIdAsync(
///             command.OperatorId,
///             cancellationToken
///         );
///         
///         if (@operator is null)
///         {
///             _logger.LogWarning(
///                 "Operator {OperatorId} not found for deletion",
///                 command.OperatorId
///             );
///             
///             return Result.Failure(
///                 Error.NotFound(
///                     "Operator.NotFound",
///                     $"Operator with ID {command.OperatorId} not found"
///                 )
///             );
///         }
///         
///         // 2. Validate business rules
///         if (@operator.IsSystemOperator())
///         {
///             _logger.LogWarning(
///                 "Attempt to delete system operator {OperatorId}",
///                 command.OperatorId
///             );
///             
///             return Result.Failure(
///                 Error.Forbidden(
///                     "Operator.CannotDeleteSystem",
///                     "System operators cannot be deleted"
///                 )
///             );
///         }
///         
///         // 3. Execute domain logic
///         // Domain event may be raised here: OperatorDeletedDomainEvent
///         _repository.Remove(@operator);
///         
///         _logger.LogInformation(
///             "Operator {OperatorId} marked for deletion",
///             command.OperatorId
///         );
///         
///         // 4. Return success
///         return Result.Success();
///         
///         // Note: UnitOfWorkBehavior will:
///         // - Dispatch OperatorDeletedDomainEvent
///         // - Commit changes to database
///         // - All in one transaction
///     }
/// }
/// </code>
/// 
/// Error Handling:
/// - Business failures: Return Result.Failure(error)
/// - Infrastructure failures: Let exceptions propagate
/// - Validation errors: Already handled by ValidationBehavior
/// 
/// Logging:
/// - Log important business decisions
/// - Log failures at Warning level
/// - Don't log sensitive data (passwords, tokens)
/// </summary>
/// <typeparam name="TCommand">Type of command to handle</typeparam>
public interface ICommandHandler<in TCommand> : IRequestHandler<TCommand, Result>
    where TCommand : ICommand
{
}

/// <summary>
/// Interface for command handlers that return data on success.
/// Implements business logic and returns Result&lt;TResponse&gt;.
/// 
/// Common Use Case:
/// Return created entity ID after successful creation operation.
/// 
/// Example Implementation:
/// <code>
/// public sealed class RegisterOperatorCommandHandler 
///     : ICommandHandler&lt;RegisterOperatorCommand, Guid&gt;
/// {
///     private readonly IOperatorRepository _repository;
///     private readonly IPasswordHasher _passwordHasher;
///     private readonly ILogger&lt;RegisterOperatorCommandHandler&gt; _logger;
///     
///     public RegisterOperatorCommandHandler(
///         IOperatorRepository repository,
///         IPasswordHasher passwordHasher,
///         ILogger&lt;RegisterOperatorCommandHandler&gt; logger)
///     {
///         _repository = repository;
///         _passwordHasher = passwordHasher;
///         _logger = logger;
///     }
///     
///     public async Task&lt;Result&lt;Guid&gt;&gt; Handle(
///         RegisterOperatorCommand command,
///         CancellationToken cancellationToken)
///     {
///         // 1. Check for duplicate username (business rule validation)
///         if (await _repository.ExistsByUsernameAsync(command.Username, cancellationToken))
///         {
///             _logger.LogWarning(
///                 "Registration failed: Username {Username} already exists",
///                 command.Username
///             );
///             
///             return Result.Failure&lt;Guid&gt;(
///                 Error.Conflict(
///                     "Operator.DuplicateUsername",
///                     $"Username '{command.Username}' is already taken"
///                 )
///             );
///         }
///         
///         // 2. Check for duplicate email
///         if (await _repository.ExistsByEmailAsync(command.Email, cancellationToken))
///         {
///             _logger.LogWarning(
///                 "Registration failed: Email {Email} already exists",
///                 command.Email
///             );
///             
///             return Result.Failure&lt;Guid&gt;(
///                 Error.Conflict(
///                     "Operator.DuplicateEmail",
///                     $"Email '{command.Email}' is already registered"
///                 )
///             );
///         }
///         
///         // 3. Hash password (infrastructure service)
///         var hashedPassword = _passwordHasher.HashPassword(command.Password);
///         
///         // 4. Execute domain logic (aggregate method)
///         var @operator = Operator.Register(
///             command.Username,
///             command.Email,
///             hashedPassword
///         );
///         
///         // Domain event raised: OperatorRegisteredDomainEvent
///         // This will be handled by domain event handler which creates OutboxMessage
///         
///         // 5. Persist entity
///         await _repository.AddAsync(@operator, cancellationToken);
///         
///         _logger.LogInformation(
///             "Operator {OperatorId} registered successfully with username {Username}",
///             @operator.Id,
///             command.Username
///         );
///         
///         // 6. Return created ID
///         return Result.Success(@operator.Id);
///         
///         // Note: UnitOfWorkBehavior will:
///         // 1. Collect domain events from @operator entity
///         // 2. Dispatch OperatorRegisteredDomainEvent synchronously
///         // 3. Domain event handler creates OutboxMessage
///         // 4. Commit both Operator and OutboxMessage atomically
///         // 5. Clear domain events from entity
///     }
/// }
/// </code>
/// 
/// Value Return Best Practices:
/// - Return entity ID (Guid) for created entities
/// - Return boolean for yes/no outcomes
/// - Return count for bulk operations
/// - Return minimal DTOs for computed results
/// - Avoid returning full entities (use queries instead)
/// 
/// Integration with Domain Events:
/// - Domain events raised in entity methods (e.g., Operator.Register)
/// - Events stored in entity's internal collection
/// - UnitOfWork collects and dispatches events before commit
/// - Domain event handlers create OutboxMessages
/// - All changes committed atomically
/// </summary>
/// <typeparam name="TCommand">Type of command to handle</typeparam>
/// <typeparam name="TResponse">Type of data returned on success</typeparam>
public interface ICommandHandler<in TCommand, TResponse>
    : IRequestHandler<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse>
{
}
