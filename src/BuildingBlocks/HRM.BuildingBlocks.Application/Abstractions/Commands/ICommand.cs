using HRM.BuildingBlocks.Application.Results;
using MediatR;

namespace HRM.BuildingBlocks.Application.Abstractions.Commands;

/// <summary>
/// Base marker interface for all commands in the CQRS pattern.
/// Used for unified command detection in pipeline behaviors (validation, logging, UnitOfWork).
/// 
/// IMPORTANT: Do NOT implement this interface directly!
/// Use ICommand or ICommand&lt;TResponse&gt; instead.
/// 
/// Purpose:
/// - Enables behaviors to detect any command type without generic constraints
/// - Simplifies pipeline behavior implementation
/// - Provides type-safe command categorization
/// 
/// Example Usage in Behaviors:
/// <code>
/// public async Task&lt;TResponse&gt; Handle(...)
/// {
///     // Detect if request is any type of command
///     if (request is ICommandBase)
///     {
///         // Apply command-specific behavior (validation, transaction, etc.)
///     }
/// }
/// </code>
/// </summary>
public interface ICommandBase
{
}

/// <summary>
/// Interface for commands that don't return data (void operations).
/// Always returns Result indicating success or failure.
/// 
/// CQRS Characteristics:
/// - Represents write operations (Create, Update, Delete)
/// - Represents user intent or business action
/// - May modify system state
/// - May raise domain events
/// - Always returns Result (never throws business exceptions)
/// 
/// Command Design Principles:
/// 1. Immutable: Use record type with init-only properties
/// 2. Intention-revealing: Name describes the action (Register, Update, Delete)
/// 3. Self-contained: Contains all data needed for operation
/// 4. Validated: FluentValidation runs before handler execution
/// 5. Idempotent when possible: Safe to retry
/// 
/// Pipeline Flow:
/// 1. Command created (e.g., DeleteOperatorCommand)
/// 2. ValidationBehavior: Validates via FluentValidation
/// 3. LoggingBehavior: Logs command execution
/// 4. CommandHandler: Executes business logic
/// 5. UnitOfWorkBehavior: Commits changes if Result.IsSuccess
/// 6. Return Result to caller
/// 
/// Examples:
/// <code>
/// // Delete operation (no return value needed)
/// public sealed record DeleteOperatorCommand : ICommand
/// {
///     public Guid OperatorId { get; init; }
/// }
/// 
/// // Deactivate operation
/// public sealed record DeactivateUserCommand : ICommand
/// {
///     public Guid UserId { get; init; }
///     public string Reason { get; init; } = string.Empty;
/// }
/// 
/// // Update operation
/// public sealed record UpdateEmployeeCommand : ICommand
/// {
///     public Guid EmployeeId { get; init; }
///     public string FirstName { get; init; } = string.Empty;
///     public string LastName { get; init; } = string.Empty;
///     public string Email { get; init; } = string.Empty;
/// }
/// 
/// // Assign role operation
/// public sealed record AssignRoleToUserCommand : ICommand
/// {
///     public Guid UserId { get; init; }
///     public Guid RoleId { get; init; }
/// }
/// </code>
/// 
/// Handler Return Values:
/// - Result.Success(): Operation completed successfully
/// - Result.Failure(error): Operation failed for business reason
/// 
/// Error Handling Philosophy:
/// - Do NOT throw exceptions for expected business failures
/// - Return Result.Failure(Error.NotFound(...)) instead of NotFoundException
/// - Return Result.Failure(Error.Conflict(...)) instead of DuplicateException
/// - Let infrastructure exceptions bubble up (database errors, network failures)
/// 
/// Transaction Handling:
/// - UnitOfWorkBehavior automatically commits if Result.IsSuccess
/// - Automatic rollback if Result.IsFailure
/// - Domain events dispatched before commit
/// - OutboxMessages created in same transaction
/// </summary>
public interface ICommand : IRequest<Result>, ICommandBase
{
}

/// <summary>
/// Interface for commands that return data on success.
/// Always returns Result&lt;TResponse&gt; wrapping the return value.
/// 
/// Use Cases:
/// - Return created entity ID (most common)
/// - Return computed value needed by caller
/// - Return summary/statistics after bulk operation
/// 
/// Type Parameter:
/// - TResponse: Type of data returned on success (e.g., Guid, int, bool, custom DTO)
/// 
/// Inherits ICommandBase:
/// - Enables unified command detection in behaviors
/// - Works seamlessly with ValidationBehavior and UnitOfWorkBehavior
/// - Treated as command in pipeline
/// 
/// Examples:
/// <code>
/// // Return created entity ID (most common pattern)
/// public sealed record RegisterOperatorCommand : ICommand&lt;Guid&gt;
/// {
///     public string Username { get; init; } = string.Empty;
///     public string Email { get; init; } = string.Empty;
///     public string Password { get; init; } = string.Empty;
/// }
/// 
/// // Return created user ID
/// public sealed record CreateUserCommand : ICommand&lt;Guid&gt;
/// {
///     public string Username { get; init; } = string.Empty;
///     public string Email { get; init; } = string.Empty;
///     public Guid EmployeeId { get; init; }
///     public ScopeLevel ScopeLevel { get; init; }
/// }
/// 
/// // Return boolean indicating role was newly assigned vs already existed
/// public sealed record AssignRoleCommand : ICommand&lt;bool&gt;
/// {
///     public Guid UserId { get; init; }
///     public Guid RoleId { get; init; }
/// }
/// 
/// // Return summary of bulk operation
/// public sealed record BulkImportEmployeesCommand : ICommand&lt;ImportSummaryDto&gt;
/// {
///     public List&lt;EmployeeImportDto&gt; Employees { get; init; } = new();
/// }
/// </code>
/// 
/// Handler Return Values:
/// - Result.Success(value): Operation succeeded, return the value
/// - Result.Failure&lt;T&gt;(error): Operation failed, return typed error
/// 
/// Value Access Pattern:
/// <code>
/// // In API controller:
/// var result = await mediator.Send(new RegisterOperatorCommand { ... });
/// 
/// if (result.IsFailure)
///     return BadRequest(result.Error);
/// 
/// // Safe to access Value here - guaranteed non-null for non-nullable types
/// var operatorId = result.Value;
/// return CreatedAtAction(nameof(GetOperator), new { id = operatorId }, null);
/// </code>
/// 
/// Design Considerations:
/// - Keep return types simple (prefer Guid over full entity)
/// - Avoid returning entities (use queries for retrieval)
/// - Return only what caller absolutely needs
/// - Consider using void (ICommand) if no return value needed
/// </summary>
/// <typeparam name="TResponse">Type of data returned on success</typeparam>
public interface ICommand<TResponse> : IRequest<Result<TResponse>>, ICommandBase
{
}
