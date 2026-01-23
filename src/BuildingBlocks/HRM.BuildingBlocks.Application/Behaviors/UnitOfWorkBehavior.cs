using HRM.BuildingBlocks.Application.Abstractions.Commands;
using HRM.BuildingBlocks.Domain.Abstractions.UnitOfWork;
using MediatR;

namespace HRM.BuildingBlocks.Application.Behaviors;

/// <summary>
/// Pipeline behavior for transactional consistency using Unit of Work pattern
///
/// Responsibilities:
/// - Route command to correct module's DbContext
/// - Commit changes after handler succeeds
/// - Ensure atomicity (all or nothing)
///
/// Position in Pipeline: FOURTH (after Audit, Logging, Validation)
/// - Only wraps handler execution
/// - Doesn't open transaction if validation fails
/// - Commits AFTER handler completes successfully
///
/// Module Resolution:
/// - Command implements IModuleCommand (which inherits IHasModuleName)
/// - Behavior uses pattern matching: request is IHasModuleName
/// - Type-safe, no reflection, better performance
///
/// Transaction Flow:
/// 1. Handler executes (loads aggregates, applies business rules)
/// 2. Handler returns success
/// 3. Behavior calls UnitOfWork.CommitAsync()
/// 4. UnitOfWork dispatches domain events (synchronous)
/// 5. Domain event handlers create integration events → OutboxMessages
/// 6. SaveChanges commits: Aggregates + OutboxMessages (atomic)
/// 7. Clear domain events
/// 8. Return response
///
/// Error Handling:
/// - Handler throws → transaction NOT committed (rollback)
/// - Validation fails → behavior never called (no transaction)
/// - SaveChanges fails → exception propagates (rollback)
///
/// Important Notes:
/// - TResponse is Result<T> (wrapped response), NOT T directly
/// - IModuleCommand<T> means IRequest<Result<T>> in MediatR pipeline
/// - Constraint changed from "TRequest : IModuleCommand<TResponse>" to ICommandBase
///   because TResponse in pipeline is Result<T>, but IModuleCommand<T> expects unwrapped T
/// - Type guard "request is IHasModuleName" ensures only module commands trigger UnitOfWork
///
/// Example:
/// <code>
/// public sealed record RegisterOperatorCommand(...) : IModuleCommand<Guid>
/// {
///     public string ModuleName => "Identity";
/// }
///
/// // Behavior automatically:
/// // 1. Resolves IdentityUnitOfWork
/// // 2. Calls handler
/// // 3. Commits transaction
/// </code>
/// </summary>
/// <typeparam name="TRequest">The request type (must be ICommandBase)</typeparam>
/// <typeparam name="TResponse">The response type (Result or Result<T>)</typeparam>
public sealed class UnitOfWorkBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommandBase
{
    private readonly IEnumerable<IModuleUnitOfWork> _unitOfWorks;

    public UnitOfWorkBehavior(IEnumerable<IModuleUnitOfWork> unitOfWorks)
    {
        _unitOfWorks = unitOfWorks;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Execute handler first
        var response = await next();

        // Type-safe guard: Check if request implements IHasModuleName (module command)
        // Uses pattern matching instead of reflection for better performance and type safety
        if (request is not IHasModuleName moduleCommand)
        {
            // Not a module command, skip UnitOfWork commit
            // This handles ICommand (non-module commands) gracefully
            return response;
        }

        // Validate ModuleName is not null or empty
        var moduleName = moduleCommand.ModuleName;
        if (string.IsNullOrEmpty(moduleName))
        {
            throw new InvalidOperationException(
                $"Command {request.GetType().Name} implements IHasModuleName but ModuleName is null or empty. " +
                $"IModuleCommand implementations must return a non-empty ModuleName.");
        }

        // Resolve module-specific UnitOfWork
        var unitOfWork = _unitOfWorks.SingleOrDefault(x => x.ModuleName == moduleName)
            ?? throw new InvalidOperationException(
                $"No UnitOfWork registered for module '{moduleName}'. " +
                $"Ensure {moduleName}Infrastructure is added to DI container. " +
                $"Command: {request.GetType().Name}");

        // Commit transaction (dispatches domain events, saves changes)
        await unitOfWork.CommitAsync(cancellationToken);

        return response;
    }
}
