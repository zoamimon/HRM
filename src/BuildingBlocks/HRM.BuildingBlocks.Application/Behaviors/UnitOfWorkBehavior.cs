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
/// - Command implements IModuleCommand with ModuleName property
/// - Behavior resolves IModuleUnitOfWork matching that module
/// - Type-safe, no string magic
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
/// - Runtime check ensures only IModuleCommand instances trigger UnitOfWork
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

        // Check if request has ModuleName property (IModuleCommand interface)
        // Use reflection to get ModuleName property dynamically
        var moduleNameProperty = request.GetType().GetProperty("ModuleName");
        if (moduleNameProperty is null)
        {
            // Not a module command, skip UnitOfWork commit
            // This handles ICommand (non-module commands) gracefully
            return response;
        }

        var moduleName = moduleNameProperty.GetValue(request) as string;
        if (string.IsNullOrEmpty(moduleName))
        {
            throw new InvalidOperationException(
                $"Command {request.GetType().Name} has ModuleName property but it's null or empty. " +
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
