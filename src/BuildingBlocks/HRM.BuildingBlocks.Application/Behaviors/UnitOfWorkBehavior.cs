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
/// Position in Pipeline: THIRD (after Logging, Validation)
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
/// <typeparam name="TRequest">The request type (must be IModuleCommand)</typeparam>
/// <typeparam name="TResponse">The response type</typeparam>
public sealed class UnitOfWorkBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IModuleCommand<TResponse>
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

        // Resolve module-specific UnitOfWork
        var unitOfWork = _unitOfWorks.SingleOrDefault(x => x.ModuleName == request.ModuleName)
            ?? throw new InvalidOperationException(
                $"No UnitOfWork registered for module '{request.ModuleName}'. " +
                $"Ensure {request.ModuleName}Infrastructure is added to DI container.");

        // Commit transaction (dispatches domain events, saves changes)
        await unitOfWork.CommitAsync(cancellationToken);

        return response;
    }
}
