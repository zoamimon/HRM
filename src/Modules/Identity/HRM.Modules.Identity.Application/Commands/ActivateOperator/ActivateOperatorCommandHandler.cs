using HRM.BuildingBlocks.Application.Abstractions.Messaging;
using HRM.BuildingBlocks.Domain.Abstractions.Results;
using HRM.Modules.Identity.Domain.Errors;
using HRM.Modules.Identity.Domain.Repositories;

namespace HRM.Modules.Identity.Application.Commands.ActivateOperator;

/// <summary>
/// Handler for ActivateOperatorCommand
/// Changes operator status from Pending to Active
///
/// Dependencies:
/// - IOperatorRepository: Retrieve and persist operator
/// - IUnitOfWork: Commit transaction (injected via UnitOfWorkBehavior)
///
/// Processing Steps:
/// 1. Retrieve operator by ID (404 if not found)
/// 2. Call operator.Activate() - domain logic
///    - Validates status (Pending → Active)
///    - Sets ActivatedAtUtc
///    - Raises OperatorActivatedDomainEvent
/// 3. Domain event dispatched automatically
/// 4. Return success
///
/// Error Handling:
/// - Result pattern: Success or Failure<Error>
/// - Validation errors: Handled by FluentValidation (ValidationBehavior)
/// - Not found: Returns OperatorErrors.NotFound
/// - Already active: Domain logic handles (idempotent)
/// - Infrastructure errors: Propagated as exceptions (logged by LoggingBehavior)
///
/// Transaction Management:
/// - UnitOfWorkBehavior wraps handler in transaction
/// - Domain events dispatched BEFORE SaveChangesAsync
/// - Integration events created in OutboxMessages table (same transaction)
/// - Rollback on any error
///
/// Performance:
/// - 1 database query (SELECT by ID)
/// - 1 UPDATE operation
/// - Total: ~10-20ms
///
/// Idempotency:
/// - Activating already active operator returns success
/// - Safe to retry on network failures
/// </summary>
internal sealed class ActivateOperatorCommandHandler : ICommandHandler<ActivateOperatorCommand>
{
    private readonly IOperatorRepository _operatorRepository;

    public ActivateOperatorCommandHandler(IOperatorRepository operatorRepository)
    {
        _operatorRepository = operatorRepository;
    }

    public async Task<Result> Handle(ActivateOperatorCommand request, CancellationToken cancellationToken)
    {
        // 1. Retrieve operator by ID
        var @operator = await _operatorRepository.GetByIdAsync(request.OperatorId, cancellationToken);

        if (@operator is null)
        {
            return Result.Failure(OperatorErrors.NotFound(request.OperatorId));
        }

        // 2. Activate operator (domain logic)
        // - Validates status transition (Pending → Active)
        // - Sets ActivatedAtUtc to current UTC time
        // - Raises OperatorActivatedDomainEvent
        // - Throws InvalidOperationException if already active (caught by domain logic)
        try
        {
            @operator.Activate();
        }
        catch (InvalidOperationException ex)
        {
            // Already active - return success (idempotent)
            // This makes the operation safe to retry
            if (ex.Message.Contains("already active"))
            {
                return Result.Success();
            }

            // Other validation errors (suspended, deactivated, etc.)
            return Result.Failure(new ValidationError("Operator.CannotActivate", ex.Message));
        }

        // 3. EF Core tracks changes automatically (no explicit Update needed)
        // UnitOfWorkBehavior calls SaveChangesAsync after handler returns
        // Domain events dispatched during SaveChanges

        // 4. Return success
        return Result.Success();
    }
}
